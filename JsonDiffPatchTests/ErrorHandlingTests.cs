using System;
using JsonDiffPatch;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tavis.JsonPatch.Tests
{
    /// <summary>
    /// Covers the failure paths: a malformed patch document, or one that does not fit its target,
    /// has to be reported rather than silently ignored or turned into a NullReferenceException.
    /// </summary>
    [TestFixture]
    public class ErrorHandlingTests
    {
        [Test]
        public void Test_operation_passes_when_the_value_matches()
        {
            JToken document = JObject.Parse(@"{""a"":{""b"":[1,2]}}");
            var patch = new PatchDocument(
                new TestOperation(new JsonPointer("/a/b"), JArray.Parse("[1,2]")),
                new TestOperation(new JsonPointer("/a/b/0"), new JValue(1)));

            Assert.DoesNotThrow(() => new JsonPatcher().Patch(ref document, patch));
        }

        [Test]
        public void Test_operation_fails_when_the_value_does_not_match()
        {
            JToken document = JObject.Parse(@"{""a"":1}");
            var patch = new PatchDocument(new TestOperation(new JsonPointer("/a"), new JValue(2)));

            Assert.Throws<InvalidOperationException>(() => new JsonPatcher().Patch(ref document, patch));
        }

        [Test]
        public void Parsing_a_document_that_is_not_an_array_is_an_error()
        {
            Assert.Throws<ArgumentException>(() => PatchDocument.Parse(@"{""op"":""add"",""path"":""/a"",""value"":1}"));
        }

        [Test]
        public void Parsing_an_unknown_operation_is_an_error()
        {
            Assert.Throws<ArgumentException>(() => PatchDocument.Parse(@"[{""op"":""frobnicate"",""path"":""/a""}]"));
            Assert.Throws<ArgumentException>(() => PatchDocument.Parse(@"[{""path"":""/a""}]"));
        }

        [Test]
        public void Parsing_an_operation_without_its_required_members_is_an_error()
        {
            Assert.Throws<ArgumentException>(() => PatchDocument.Parse(@"[{""op"":""add"",""value"":1}]"));
            Assert.Throws<ArgumentException>(() => PatchDocument.Parse(@"[{""op"":""add"",""path"":""/a""}]"));
            Assert.Throws<ArgumentException>(() => PatchDocument.Parse(@"[{""op"":""move"",""path"":""/a""}]"));
            Assert.DoesNotThrow(() => PatchDocument.Parse(@"[{""op"":""add"",""path"":""/a"",""value"":null}]"),
                "an explicit null is a legal value, unlike an absent one");
        }

        [Test]
        public void Adding_at_an_out_of_range_array_index_is_an_error()
        {
            JToken document = JObject.Parse(@"{""a"":[1,2]}");
            var patch = PatchDocument.Parse(@"[{""op"":""add"",""path"":""/a/9"",""value"":3}]");

            Assert.Throws<ArgumentException>(() => new JsonPatcher().Patch(ref document, patch));
        }

        [Test]
        public void Adding_at_the_document_root_is_an_error()
        {
            JToken document = JObject.Parse(@"{""a"":1}");
            var patch = new PatchDocument(new AddOperation(new JsonPointer(""), JObject.Parse(@"{""b"":2}")));

            Assert.Throws<ArgumentException>(() => new JsonPatcher().Patch(ref document, patch));
        }

        [Test]
        public void Removing_a_path_that_does_not_exist_is_an_error()
        {
            JToken document = JObject.Parse(@"{""a"":1}");
            var patch = PatchDocument.Parse(@"[{""op"":""remove"",""path"":""/zzz""}]");

            var error = Assert.Throws<ArgumentException>(() => new JsonPatcher().Patch(ref document, patch));
            StringAssert.Contains("zzz", error.Message);
        }

        [Test]
        public void Moving_to_a_sibling_whose_name_starts_with_the_source_name_is_allowed()
        {
            JToken document = JObject.Parse(@"{""a"":{""b"":1}}");
            var patch = PatchDocument.Parse(@"[{""op"":""move"",""from"":""/a"",""path"":""/ab""}]");

            new JsonPatcher().Patch(ref document, patch);

            Assert.IsTrue(JToken.DeepEquals(document, JObject.Parse(@"{""ab"":{""b"":1}}")));
        }

        [Test]
        public void Moving_a_node_into_itself_is_an_error()
        {
            JToken document = JObject.Parse(@"{""a"":{""b"":1}}");
            var patch = PatchDocument.Parse(@"[{""op"":""move"",""from"":""/a"",""path"":""/a/c""}]");

            Assert.Throws<ArgumentException>(() => new JsonPatcher().Patch(ref document, patch));
        }

        [Test]
        public void Operations_cannot_be_mutated_through_the_read_only_view()
        {
            var document = new PatchDocument();

            Assert.IsNotInstanceOf<System.Collections.Generic.List<Operation>>(document.Operations);
        }
    }
}
