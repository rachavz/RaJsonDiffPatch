using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Tavis;

namespace JsonDiffPatch
{
    /// <summary>
    /// Compares two JSON tokens and generates a JSON Patch document describing the differences.
    /// Parts adapted from https://github.com/benjamine/jsondiffpatch/blob/42ce1b6ca30c4d7a19688a020fce021a756b43cc/src/filters/arrays.js
    /// </summary>
    public class JsonDiffer
    {
        /// <summary>
        /// Computes the diff between two JSON tokens and returns a <see cref="PatchDocument"/> describing the changes.
        /// </summary>
        /// <param name="from">The original JSON token.</param>
        /// <param name="to">The target JSON token.</param>
        /// <param name="useIdPropertyToDetermineEquality">
        /// If <c>true</c>, two objects inside an array are treated as the same element when their "id" properties match,
        /// and are then diffed member by member instead of being removed and re-added.
        /// </param>
        /// <returns>A <see cref="PatchDocument"/> containing the operations to transform <paramref name="from"/> into <paramref name="to"/>.</returns>
        public PatchDocument Diff(JToken @from, JToken to, bool useIdPropertyToDetermineEquality)
        {
            if (@from == null) throw new ArgumentNullException(nameof(@from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            var operations = new List<Operation>();
            Diff(@from, to, useIdPropertyToDetermineEquality, new string[0], operations);
            return new PatchDocument(operations.ToArray());
        }

        /// <summary>
        /// Appends the operations that turn <paramref name="left"/> into <paramref name="right"/> to <paramref name="output"/>.
        /// </summary>
        /// <remarks>
        /// The current location is carried as decoded reference tokens rather than an encoded pointer string, so
        /// operations are built straight from the tokens without an encode/decode round trip per operation.
        /// </remarks>
        private static void Diff(JToken left, JToken right, bool useId, string[] path, List<Operation> output)
        {
            if (left.Type != right.Type)
            {
                output.Add(new ReplaceOperation(new JsonPointer(path), right.DeepClone()));
                return;
            }

            switch (left.Type)
            {
                case JTokenType.Array:
                    int start = output.Count;
                    DiffArray((JArray)left, (JArray)right, useId, path, output);
                    CoalesceRemoveAdd(output, start);
                    break;

                case JTokenType.Object:
                    DiffObject((JObject)left, (JObject)right, useId, path, output);
                    break;

                default:
                    if (!JToken.DeepEquals(left, right))
                    {
                        output.Add(new ReplaceOperation(new JsonPointer(path), right.DeepClone()));
                    }
                    break;
            }
        }

        private static void DiffObject(JObject left, JObject right, bool useId, string[] path, List<Operation> output)
        {
            // Sorted so that the emitted order does not depend on the member order of either document.
            var lprops = SortedProperties(left);
            var rprops = SortedProperties(right);

            JToken other;
            foreach (var prop in lprops)
            {
                if (!right.TryGetValue(prop.Name, out other))
                {
                    output.Add(new RemoveOperation(new JsonPointer(Append(path, prop.Name))));
                }
            }

            foreach (var prop in rprops)
            {
                if (!left.TryGetValue(prop.Name, out other))
                {
                    output.Add(new AddOperation(new JsonPointer(Append(path, prop.Name)), prop.Value.DeepClone()));
                }
            }

            foreach (var prop in lprops)
            {
                if (right.TryGetValue(prop.Name, out other))
                {
                    Diff(prop.Value, other, useId, Append(path, prop.Name), output);
                }
            }
        }

        private static JProperty[] SortedProperties(JObject obj)
        {
            var props = new JProperty[obj.Count];
            int n = 0;
            foreach (var child in obj.Children())
            {
                props[n++] = (JProperty)child;
            }

            Array.Sort(props, PropertyNameComparer.Instance);
            return props;
        }

        private static void DiffArray(JArray left, JArray right, bool useId, string[] path, List<Operation> output)
        {
            int len1 = left.Count;
            int len2 = right.Count;

            int head = 0;
            while (head < len1 && head < len2 && SameElement(left[head], right[head], useId, path, head, output))
            {
                head++;
            }

            int tail = 0;
            while (tail + head < len1 && tail + head < len2)
            {
                var index1 = len1 - 1 - tail;
                if (!SameElement(left[index1], right[len2 - 1 - tail], useId, path, index1, output)) break;
                tail++;
            }

            if (head == 0 && tail == 0 && len1 > 0 && len2 > 0)
            {
                output.Add(new ReplaceOperation(new JsonPointer(path), right.DeepClone()));
                return;
            }

            int middle1 = len1 - head - tail;
            int middle2 = len2 - head - tail;

            if (middle1 == middle2)
            {
                // A middle of equal length is diffed element by element. Most elements in a list of
                // records are untouched, and a deep-equality check is far cheaper than diffing them.
                for (int i = 0; i < middle1; i++)
                {
                    var l = left[head + i];
                    var r = right[head + i];
                    if (JToken.DeepEquals(l, r)) continue;

                    Diff(l, r, useId, Append(path, head + i), output);
                }

                return;
            }

            // Every removal happens at the same index, because each one shifts the rest of the middle down.
            var removeAt = new JsonPointer(Append(path, head));
            for (int i = 0; i < middle1; i++)
            {
                output.Add(new RemoveOperation(removeAt));
            }

            for (int i = 0; i < middle2; i++)
            {
                output.Add(new AddOperation(new JsonPointer(Append(path, head + i)), right[head + i].DeepClone()));
            }
        }

        /// <summary>
        /// Decides whether two array elements at the same position line up. Deep-equal elements match and need no
        /// operations. With <paramref name="useId"/>, objects sharing an "id" also match, and the operations that
        /// reconcile their contents are emitted here.
        /// </summary>
        private static bool SameElement(JToken left, JToken right, bool useId, string[] path, int index, List<Operation> output)
        {
            if (JToken.DeepEquals(left, right)) return true;

            if (useId && left.Type == JTokenType.Object && right.Type == JTokenType.Object)
            {
                var id = GetId(left);
                if (id != null && id == GetId(right))
                {
                    Diff(left, right, true, Append(path, index), output);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads an object's "id" property as a string, or <c>null</c> when it has none, it is JSON null,
        /// or it is not a scalar.
        /// </summary>
        private static string GetId(JToken token)
        {
            var id = token["id"] as JValue;
            if (id == null || id.Value == null) return null;

            return Convert.ToString(id.Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Rewrites a remove immediately followed by an add at the same path, which is how an array's middle is
        /// rebuilt, into a single replace.
        /// </summary>
        private static void CoalesceRemoveAdd(List<Operation> output, int start)
        {
            int write = start;
            for (int read = start; read < output.Count; read++)
            {
                var add = output[read] as AddOperation;
                if (add != null && write > start)
                {
                    var previous = output[write - 1] as RemoveOperation;
                    if (previous != null && previous.Path.Equals(add.Path))
                    {
                        output[write - 1] = new ReplaceOperation(add.Path, add.Value);
                        continue;
                    }
                }

                output[write++] = output[read];
            }

            output.RemoveRange(write, output.Count - write);
        }

        private static string[] Append(string[] path, string token)
        {
            var result = new string[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = token;
            return result;
        }

        private static string[] Append(string[] path, int index)
        {
            return Append(path, index.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Orders properties by name using the same culture-sensitive comparison as <c>OrderBy</c>, which is what
        /// earlier versions emitted; changing it would reorder existing patch output.
        /// </summary>
        private sealed class PropertyNameComparer : IComparer<JProperty>
        {
            public static readonly PropertyNameComparer Instance = new PropertyNameComparer();

            public int Compare(JProperty x, JProperty y) => Comparer<string>.Default.Compare(x.Name, y.Name);
        }
    }
}
