using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace RaJsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch "replace" operation that replaces the value at the target path.
    /// </summary>
    public class ReplaceOperation : Operation
    {
        /// <summary>
        /// Gets the replacement value.
        /// </summary>
        public JToken Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceOperation"/> class.
        /// </summary>
        public ReplaceOperation()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceOperation"/> class with the specified path and value.
        /// </summary>
        /// <param name="path">The JSON Pointer path of the value to replace.</param>
        /// <param name="value">The replacement value.</param>
        public ReplaceOperation(JsonPointer path, JToken value) : base(path)
        {
            Value = value;
        }

        /// <inheritdoc />
        public override void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteOp(writer, "replace");
            WritePath(writer, Path);
            WriteValue(writer, Value);

            writer.WriteEndObject();
        }

        /// <inheritdoc />
        public override void Read(JObject jOperation)
        {
            Path = new JsonPointer(SplitPath((string)jOperation.GetValue("path")));
            Value = jOperation.GetValue("value");
        }
    }
}
