using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace JsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch "remove" operation that removes a value at the target path.
    /// </summary>
    public class RemoveOperation : Operation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoveOperation"/> class.
        /// </summary>
        public RemoveOperation()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoveOperation"/> class with the specified path.
        /// </summary>
        /// <param name="path">The JSON Pointer path of the value to remove.</param>
        public RemoveOperation(JsonPointer path) : base(path)
        {
        }

        /// <inheritdoc />
        public override void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteOp(writer, "remove");
            WritePath(writer, Path);

            writer.WriteEndObject();
        }

        /// <inheritdoc />
        public override void Read(JObject jOperation)
        {
            Path = ReadPointer(jOperation, "path");
        }
    }
}
