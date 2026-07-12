using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace RaJsonDiffPatch
{
    /// <summary>
    /// Represents a single JSON Patch operation as defined by RFC 6902.
    /// </summary>
    public abstract class Operation
    {
        /// <summary>
        /// Gets the JSON Pointer path that this operation targets.
        /// </summary>
        public JsonPointer Path { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Operation"/> class.
        /// </summary>
        public Operation()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Operation"/> class with the specified path.
        /// </summary>
        /// <param name="path">The JSON Pointer path targeted by this operation.</param>
        public Operation(JsonPointer path)
        {
            Path = path;
        }

        /// <summary>
        /// Serializes this operation to the specified JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer to serialize to.</param>
        public abstract void Write(JsonWriter writer);

        /// <summary>
        /// Writes the "op" property to the JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="op">The operation name (e.g. "add", "remove").</param>
        protected static void WriteOp(JsonWriter writer, string op)
        {
            writer.WritePropertyName("op");
            writer.WriteValue(op);
        }

        /// <summary>
        /// Writes the "path" property to the JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="pointer">The JSON Pointer to write as the path value.</param>
        protected static void WritePath(JsonWriter writer, JsonPointer pointer)
        {
            writer.WritePropertyName("path");
            writer.WriteValue(pointer.ToString());
        }

        /// <summary>
        /// Writes the "from" property to the JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="pointer">The JSON Pointer to write as the from value.</param>
        protected static void WriteFromPath(JsonWriter writer, JsonPointer pointer)
        {
            writer.WritePropertyName("from");
            writer.WriteValue(pointer.ToString());
        }

        /// <summary>
        /// Writes the "value" property to the JSON writer.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The JToken value to write.</param>
        protected static void WriteValue(JsonWriter writer, JToken value)
        {
            writer.WritePropertyName("value");
            value.WriteTo(writer);
        }

        /// <summary>
        /// Splits a JSON Pointer string into its individual tokens, skipping the leading empty segment.
        /// </summary>
        /// <param name="path">The JSON Pointer string (e.g. "/foo/bar").</param>
        /// <returns>An array of path tokens.</returns>
        protected static string[] SplitPath(string path) => path.Split('/').Skip(1).ToArray();

        /// <summary>
        /// Deserializes this operation from the specified JSON object.
        /// </summary>
        /// <param name="jOperation">The JSON object representing the operation.</param>
        public abstract void Read(JObject jOperation);

        /// <summary>
        /// Parses a JSON string into an <see cref="Operation"/>.
        /// </summary>
        /// <param name="json">A JSON string representing a single patch operation.</param>
        /// <returns>The parsed <see cref="Operation"/>.</returns>
        public static Operation Parse(string json)
        {
            return Build(JObject.Parse(json));
        }

        /// <summary>
        /// Builds an <see cref="Operation"/> from a JSON object.
        /// </summary>
        /// <param name="jOperation">The JSON object representing the operation.</param>
        /// <returns>The constructed <see cref="Operation"/>.</returns>
        public static Operation Build(JObject jOperation)
        {
            var op = PatchDocument.CreateOperation((string)jOperation["op"]);
            op.Read(jOperation);
            return op;
        }
    }
}
