using System;
using Newtonsoft.Json.Linq;
using Tavis;

namespace JsonDiffPatch
{
    /// <summary>
    /// Applies JSON Patch operations to <see cref="JToken"/> documents.
    /// </summary>
    public class JsonPatcher : AbstractPatcher<JToken>
    {

        /// <inheritdoc />
        protected override JToken Replace(ReplaceOperation operation, JToken target)
        {
            var token = operation.Path.Find(target);
            if (token.Parent == null)
            {
                return operation.Value;
            }
            else
            {
                token.Replace(operation.Value);
                return null;
            }
        }

        /// <inheritdoc />
        protected override void Add(AddOperation operation, JToken target)
        {
            Add(operation.Path, operation.Value, target);
        }

        private static void Add(JsonPointer path, JToken value, JToken target)
        {
            if (path.Count == 0)
                throw new ArgumentException(
                    "Cannot 'add' at the root of the document; use 'replace' instead.", nameof(path));

            var name = path.LastToken;
            var parent = path.Find(target, path.Count - 1);

            var parentArray = parent as JArray;
            if (parentArray != null)
            {
                if (name == "-")
                {
                    parentArray.Add(value);
                    return;
                }

                int index;
                if (!JsonPointer.TryParseIndex(name, out index) || index > parentArray.Count)
                    throw new ArgumentException(
                        "'" + name + "' is not a valid insertion index into the array of " + parentArray.Count +
                        " element(s) at '" + path.ParentPointer + "'.", nameof(path));

                parentArray.Insert(index, value);
                return;
            }

            var parentObject = parent as JObject;
            if (parentObject == null)
                throw new ArgumentException(
                    "Cannot add '" + name + "': the value at '" + path.ParentPointer + "' is a " + parent.Type +
                    ", not an object or an array.", nameof(path));

            // Adding to a path that already holds an array appends to that array rather than replacing it.
            // That is not what RFC 6902 says, but it is this library's long-standing behaviour and callers
            // (including the "/books" style adds in the test suite) depend on it.
            var existingArray = parentObject[name] as JArray;
            if (existingArray != null)
            {
                existingArray.Add(value);
                return;
            }

            parentObject[name] = value;
        }

        /// <inheritdoc />
        protected override void Remove(RemoveOperation operation, JToken target)
        {
            Remove(operation.Path, target);
        }

        private static void Remove(JsonPointer path, JToken target)
        {
            var token = path.Find(target);
            if (token.Parent == null)
                throw new ArgumentException(
                    "Cannot 'remove' the root of the document.", nameof(path));

            if (token.Parent is JProperty)
            {
                token.Parent.Remove();
            }
            else
            {
                token.Remove();
            }
        }

        /// <inheritdoc />
        protected override void Move(MoveOperation operation, JToken target)
        {
            if (operation.Path.IsSelfOrDescendantOf(operation.FromPath))
                throw new ArgumentException(
                    "Cannot move '" + operation.FromPath + "' into itself ('" + operation.Path + "').",
                    nameof(operation));

            var token = operation.FromPath.Find(target);
            Remove(operation.FromPath, target);
            Add(operation.Path, token, target);
        }

        /// <inheritdoc />
        protected override void Test(TestOperation operation, JToken target)
        {
            var existingValue = operation.Path.Find(target);
            if (!JToken.DeepEquals(existingValue, operation.Value))
            {
                throw new InvalidOperationException("Value at " + operation.Path + " does not match.");
            }
        }

        /// <inheritdoc />
        protected override void Copy(CopyOperation operation, JToken target)
        {
            var token = operation.FromPath.Find(target);
            Add(operation.Path, token.DeepClone(), target);
        }
    }
}
