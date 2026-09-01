using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace JsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch "test" operation that checks that a value at the target path equals the specified value.
    /// </summary>
    public class TestOperation : Operation
    {
        /// <summary>
        /// Gets the value to test against.
        /// </summary>
        public JToken Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestOperation"/> class.
        /// </summary>
        public TestOperation()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestOperation"/> class with the specified path and value.
        /// </summary>
        /// <param name="path">The JSON Pointer path to test.</param>
        /// <param name="value">The expected value at the target path.</param>
        public TestOperation(JsonPointer path, JToken value) : base(path)
        {
            Value = value;
        }

        /// <inheritdoc />
        public override void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteOp(writer, "test");
            WritePath(writer, Path);
            WriteValue(writer, Value);

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
