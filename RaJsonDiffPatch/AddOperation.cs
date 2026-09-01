using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace JsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch "add" operation that adds a value to an object or inserts into an array.
    /// </summary>
    public class AddOperation : Operation
    {
        /// <summary>
        /// Gets the value to add.
        /// </summary>
        public JToken Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddOperation"/> class.
        /// </summary>
        public AddOperation()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddOperation"/> class with the specified path and value.
        /// </summary>
        /// <param name="path">The JSON Pointer path where the value should be added.</param>
        /// <param name="value">The value to add.</param>
        public AddOperation(JsonPointer path, JToken value) : base(path)
        {
            Value = value;
        }

        /// <inheritdoc />
        public override void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteOp(writer, "add");
            WritePath(writer,Path);
            WriteValue(writer,Value);

            writer.WriteEndObject();
        }

        /// <inheritdoc />
        public override void Read(JObject jOperation)
        {

            Path = ReadPointer(jOperation, "path");
            Value = ReadValue(jOperation);
        }
    }
}
