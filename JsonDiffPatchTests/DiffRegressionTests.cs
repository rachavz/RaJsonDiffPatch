using System;
using System.Collections.Generic;
using JsonDiffPatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tavis.JsonPatch.Tests
{
    [TestFixture]
    public class DiffRegressionTests
    {
        private static string DiffJson(JToken from, JToken to, bool useId = false)
        {
            return new JsonDiffer().Diff(from, to, useId).ToString(Formatting.None);
        }

        [Test]
        public void Property_named_empty_string_is_addressed_as_slash_not_root()
        {
            var from = JToken.Parse("{\"\":1,\"a\":2}");
            var to = JToken.Parse("{\"a\":2}");

            Assert.That(DiffJson(from, to), Is.EqualTo("[{\"op\":\"remove\",\"path\":\"/\"}]"));

            var added = DiffJson(to, from);
            Assert.That(added, Is.EqualTo("[{\"op\":\"add\",\"path\":\"/\",\"value\":1}]"));

            var patched = to.DeepClone();
            new JsonPatcher().Patch(ref patched, PatchDocument.Parse(added));
            Assert.That(JToken.DeepEquals(patched, from), Is.True);
        }

        [Test]
        public void Changed_byte_array_value_is_detected()
        {
            var from = new JObject { ["blob"] = new JValue(new byte[] { 1, 2, 3 }) };
            var to = new JObject { ["blob"] = new JValue(new byte[] { 4, 5, 6 }) };

            Assert.That(new JsonDiffer().Diff(from, to, false).Operations.Count, Is.EqualTo(1));
            Assert.That(new JsonDiffer().Diff(from, from.DeepClone(), false).Operations.Count, Is.EqualTo(0));
        }

        [Test]
        public void Sub_second_date_change_is_detected()
        {
            var from = new JObject { ["at"] = new DateTime(2026, 9, 2, 12, 0, 0, 100, DateTimeKind.Utc) };
            var to = new JObject { ["at"] = new DateTime(2026, 9, 2, 12, 0, 0, 900, DateTimeKind.Utc) };

            Assert.That(new JsonDiffer().Diff(from, to, false).Operations.Count, Is.EqualTo(1));
        }

        [Test]
        public void Integer_and_float_of_same_magnitude_are_still_a_replace()
        {
            Assert.That(DiffJson(JToken.Parse("{\"n\":1}"), JToken.Parse("{\"n\":1.0}")),
                Is.EqualTo("[{\"op\":\"replace\",\"path\":\"/n\",\"value\":1.0}]"));
        }

        [Test]
        public void Patch_values_do_not_alias_the_target_document()
        {
            var from = JToken.Parse("{\"a\":[1,2],\"b\":{\"c\":1}}");
            var to = JToken.Parse("{\"a\":[1,2,3],\"b\":{\"c\":2},\"d\":{\"e\":5}}");

            var patch = new JsonDiffer().Diff(from, to, false);
            var expected = patch.ToString(Formatting.None);

            ((JArray)to["a"])[2] = 99;
            to["b"]["c"] = 99;
            to["d"]["e"] = 99;

            Assert.That(patch.ToString(Formatting.None), Is.EqualTo(expected));
        }

        [Test]
        public void Elements_matched_by_id_are_diffed_in_place()
        {
            var from = JToken.Parse("[{\"id\":1,\"v\":\"a\"},{\"id\":2,\"v\":\"b\"},{\"id\":3,\"v\":\"c\"}]");
            var to = JToken.Parse("[{\"id\":1,\"v\":\"a\"},{\"id\":2,\"v\":\"B\"},{\"id\":3,\"v\":\"c\"}]");

            Assert.That(DiffJson(from, to, useId: true),
                Is.EqualTo("[{\"op\":\"replace\",\"path\":\"/1/v\",\"value\":\"B\"}]"));
        }

        [Test]
        public void Null_arguments_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonDiffer().Diff(null, new JObject(), false));
            Assert.Throws<ArgumentNullException>(() => new JsonDiffer().Diff(new JObject(), null, false));
        }

        /// <summary>
        /// Random documents, random edits: every generated patch must serialize, parse back, and turn the
        /// original into the edited document. Seeds are fixed so a failure is reproducible.
        /// </summary>
        [Test]
        public void Generated_patches_roundtrip_for_random_documents()
        {
            var differ = new JsonDiffer();
            var patcher = new JsonPatcher();

            for (int seed = 0; seed < 3000; seed++)
            {
                var rnd = new Random(seed);
                var from = RandomValue(rnd, 4);
                var to = Mutate(rnd, from, rnd.Next(1, 6));
                bool useId = rnd.Next(2) == 0;

                var json = differ.Diff(from, to, useId).ToString(Formatting.None);
                var work = from.DeepClone();
                patcher.Patch(ref work, PatchDocument.Parse(json));

                Assert.That(JToken.DeepEquals(work, to), Is.True,
                    $"seed {seed}\nfrom:  {from.ToString(Formatting.None)}\nto:    {to.ToString(Formatting.None)}\n" +
                    $"patch: {json}\ngot:   {work.ToString(Formatting.None)}");
            }
        }

        private static readonly string[] Keys = { "a", "b", "c", "id", "name", "x/y", "t~1", "Name", "age", "", "0", "1" };

        private static JToken RandomValue(Random rnd, int depth)
        {
            switch (rnd.Next(depth <= 0 ? 4 : 7))
            {
                case 0: return rnd.Next(5);
                case 1: return "s" + rnd.Next(4);
                case 2: return rnd.Next(2) == 0;
                case 3: return JValue.CreateNull();
                case 4: return rnd.Next(3) * 0.5;
                case 5:
                {
                    var array = new JArray();
                    int n = rnd.Next(5);
                    for (int i = 0; i < n; i++) array.Add(RandomValue(rnd, depth - 1));
                    return array;
                }
                default:
                {
                    var obj = new JObject();
                    int n = rnd.Next(5);
                    for (int i = 0; i < n; i++) obj[Keys[rnd.Next(Keys.Length)]] = RandomValue(rnd, depth - 1);
                    return obj;
                }
            }
        }

        private static JToken Mutate(Random rnd, JToken original, int steps)
        {
            var doc = original.DeepClone();
            for (int s = 0; s < steps; s++)
            {
                var all = new List<JToken>();
                Collect(doc, all);
                var target = all[rnd.Next(all.Count)];
                if (target.Parent == null)
                {
                    doc = RandomValue(rnd, 2);
                    continue;
                }

                switch (rnd.Next(4))
                {
                    case 0:
                        target.Replace(RandomValue(rnd, 2));
                        break;
                    case 1:
                        if (target.Parent is JProperty property) property.Remove();
                        else target.Remove();
                        break;
                    case 2:
                        if (target is JArray array) array.Insert(rnd.Next(array.Count + 1), RandomValue(rnd, 1));
                        else if (target is JObject obj) obj[Keys[rnd.Next(Keys.Length)]] = RandomValue(rnd, 1);
                        break;
                    default:
                        if (target is JObject withId && withId["id"] == null) withId["id"] = rnd.Next(3);
                        break;
                }
            }

            return doc;
        }

        private static void Collect(JToken token, List<JToken> into)
        {
            into.Add(token);
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties()) Collect(property.Value, into);
            }
            else if (token is JArray array)
            {
                foreach (var item in array) Collect(item, into);
            }
        }
    }
}
