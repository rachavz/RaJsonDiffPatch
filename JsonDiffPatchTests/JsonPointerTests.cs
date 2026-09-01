using System;
using System.Linq;
using JsonDiffPatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tavis.JsonPatch.Tests
{
    /// <summary>
    /// Covers RFC 6901 reference-token escaping, which the diff/patch round trip depends on.
    /// </summary>
    [TestFixture]
    public class JsonPointerTests
    {
        [TestCase("/a~1b", "a/b", TestName = "Pointer decodes ~1 to a slash")]
        [TestCase("/a~0b", "a~b", TestName = "Pointer decodes ~0 to a tilde")]
        [TestCase("/a~01b", "a~1b", TestName = "Pointer decodes ~01 to a literal ~1")]
        [TestCase("/100%25", "100%25", TestName = "Pointer leaves percent sequences alone")]
        [TestCase("/a b", "a b", TestName = "Pointer leaves spaces alone")]
        public void Pointer_resolves_an_escaped_property_name(string pointer, string propertyName)
        {
            var document = new JObject { { propertyName, 42 } };

            Assert.AreEqual(42, (int)new JsonPointer(pointer).Find(document));
        }

        [TestCase("")]
        [TestCase("/a~1b")]
        [TestCase("/a~0b")]
        [TestCase("/a~01b")]
        [TestCase("/books/0/title")]
        public void Pointer_round_trips_through_its_string_form(string pointer)
        {
            Assert.AreEqual(pointer, new JsonPointer(pointer).ToString());
        }

        [Test]
        public void Root_pointer_is_the_empty_string_and_is_distinct_from_the_empty_property_name()
        {
            Assert.AreEqual("", new JsonPointer("").ToString());
            Assert.AreEqual("/", new JsonPointer("/").ToString());

            var document = JToken.Parse(@"{"""":1}");
            Assert.AreSame(document, new JsonPointer("").Find(document));
            Assert.AreEqual(1, (int)new JsonPointer("/").Find(document));
        }

        [Test]
        public void Pointer_rejects_a_string_that_is_not_a_pointer()
        {
            Assert.Throws<ArgumentException>(() => new JsonPointer("books/0"));
        }

        [Test]
        public void Root_pointer_has_no_parent_and_is_not_a_new_pointer()
        {
            var root = new JsonPointer("");

            Assert.IsNull(root.ParentPointer);
            Assert.IsFalse(root.IsNewPointer());
        }

        [TestCase("a/b", TestName = "Diff escapes a slash in a property name")]
        [TestCase("a~b", TestName = "Diff escapes a tilde in a property name")]
        [TestCase("a~1b", TestName = "Diff escapes a literal ~1 in a property name")]
        public void Diff_of_an_escaped_property_survives_serialization(string propertyName)
        {
            foreach (var value in new JToken[] { new JValue(2), JArray.Parse("[9,8,7]"), JObject.Parse("{'x':1}") })
            {
                var left = new JObject { { propertyName, JValue.CreateNull() } };
                var right = new JObject { { propertyName, value } };

                var patch = new JsonDiffer().Diff(left, right, false);

                // The pointer has to survive a trip through text: this is what a patch document is for.
                var reparsed = PatchDocument.Parse(patch.ToString(Formatting.None));

                JToken patched = left.DeepClone();
                new JsonPatcher().Patch(ref patched, reparsed);

                Assert.IsTrue(JToken.DeepEquals(patched, right),
                    "expected " + right.ToString(Formatting.None) + " but got " + patched.ToString(Formatting.None) +
                    " via " + patch.ToString(Formatting.None));
            }
        }

        [Test]
        public void Diff_emits_the_same_pointer_for_a_scalar_and_for_an_array()
        {
            var scalar = new JsonDiffer().Diff(
                JObject.Parse(@"{""a/b"":1}"), JObject.Parse(@"{""a/b"":2}"), false);
            var array = new JsonDiffer().Diff(
                JObject.Parse(@"{""a/b"":[1]}"), JObject.Parse(@"{""a/b"":[9,8]}"), false);

            Assert.AreEqual("/a~1b", scalar.Operations.First().Path.ToString());
            Assert.AreEqual("/a~1b", array.Operations.First().Path.ToString());
        }
    }
}
