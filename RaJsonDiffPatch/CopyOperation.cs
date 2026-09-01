using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace JsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch "copy" operation that copies a value from one location to another.
    /// </summary>
    public class CopyOperation : Operation
    {
        /// <summary>
        /// Gets the source JSON Pointer path to copy from.
        /// </summary>
        public JsonPointer FromPath { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyOperation"/> class.
        /// </summary>
        public CopyOperation()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyOperation"/> class with the specified paths.
        /// </summary>
        /// <param name="path">The destination JSON Pointer path.</param>
        /// <param name="fromPath">The source JSON Pointer path to copy from.</param>
        public CopyOperation(JsonPointer path, JsonPointer fromPath) : base(path)
        {
            FromPath = fromPath;
        }

        /// <inheritdoc />
        public override void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteOp(writer, "copy");
            WritePath(writer, Path);
            WriteFromPath(writer, FromPath);

            writer.WriteEndObject();
        }

        /// <inheritdoc />
        public override void Read(JObject jOperation)
        {
            Path = ReadPointer(jOperation, "path");
            FromPath = ReadPointer(jOperation, "from");
        }
    }
}
