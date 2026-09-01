using System;
using System.Globalization;
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
            var parentPointer = operation.Path.ParentPointer;
            if (parentPointer == null)
                throw new ArgumentException(
                    "Cannot 'add' at the root of the document; use 'replace' instead.", nameof(operation));

            var name = operation.Path.LastToken;
            var parent = parentPointer.Find(target);

            var parentArray = parent as JArray;
            if (parentArray != null)
            {
                if (name == "-")
                {
                    parentArray.Add(operation.Value);
                    return;
                }

                int index;
                if (!TryParseIndex(name, out index) || index > parentArray.Count)
                    throw new ArgumentException(
                        "'" + name + "' is not a valid insertion index into the array of " + parentArray.Count +
                        " element(s) at '" + parentPointer + "'.", nameof(operation));

                parentArray.Insert(index, operation.Value);
                return;
            }

            var parentObject = parent as JObject;
            if (parentObject == null)
                throw new ArgumentException(
                    "Cannot add '" + name + "': the value at '" + parentPointer + "' is a " + parent.Type +
                    ", not an object or an array.", nameof(operation));

            // Adding to a path that already holds an array appends to that array rather than replacing it.
            // That is not what RFC 6902 says, but it is this library's long-standing behaviour and callers
            // (including the "/books" style adds in the test suite) depend on it.
            var existingArray = parentObject[name] as JArray;
            if (existingArray != null)
            {
                existingArray.Add(operation.Value);
                return;
            }

            parentObject[name] = operation.Value;
        }

        /// <summary>
        /// Parses an array index, rejecting the signs, whitespace and leading zeroes that RFC 6901 disallows.
        /// </summary>
        private static bool TryParseIndex(string token, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(token)) return false;
            if (token.Length > 1 && token[0] == '0') return false;

            return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index);
        }

        /// <inheritdoc />
        protected override void Remove(RemoveOperation operation, JToken target)
        {
            var token = operation.Path.Find(target);
            if (token.Parent == null)
                throw new ArgumentException(
                    "Cannot 'remove' the root of the document.", nameof(operation));

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
            if (IsSelfOrDescendantOf(operation.Path, operation.FromPath))
                throw new ArgumentException(
                    "Cannot move '" + operation.FromPath + "' into itself ('" + operation.Path + "').",
                    nameof(operation));

            var token = operation.FromPath.Find(target);
            Remove(new RemoveOperation(operation.FromPath), target);
            Add(new AddOperation(operation.Path, token), target);
        }

        /// <summary>
        /// Determines whether <paramref name="path"/> is <paramref name="ancestor"/> itself or nested inside it.
        /// </summary>
        /// <remarks>
        /// Compares whole reference tokens, so "/ab" is not treated as being inside "/a" the way a plain
        /// string prefix test would. Both pointers render their separators unambiguously, because
        /// <see cref="JsonPointer.ToString"/> escapes any '/' occurring inside a token.
        /// </remarks>
        private static bool IsSelfOrDescendantOf(JsonPointer path, JsonPointer ancestor)
        {
            var a = ancestor.ToString();
            var p = path.ToString();

            if (p.Length < a.Length) return false;
            if (!p.StartsWith(a, StringComparison.Ordinal)) return false;

            return p.Length == a.Length || p[a.Length] == '/';
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
            Add(new AddOperation(operation.Path, token.DeepClone()), target);
        }
    }
}
