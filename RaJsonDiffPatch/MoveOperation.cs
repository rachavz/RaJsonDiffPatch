using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tavis;

namespace RaJsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch "move" operation that moves a value from one location to another.
    /// </summary>
    public class MoveOperation : Operation
    {
        /// <summary>
        /// Gets the source JSON Pointer path to move from.
        /// </summary>
        public JsonPointer FromPath { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveOperation"/> class.
        /// </summary>
        public MoveOperation()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveOperation"/> class with the specified paths.
        /// </summary>
        /// <param name="path">The destination JSON Pointer path.</param>
        /// <param name="fromPath">The source JSON Pointer path to move from.</param>
        public MoveOperation(JsonPointer path, JsonPointer fromPath) : base(path)
        {
            FromPath = fromPath;
        }

        /// <inheritdoc />
        public override void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteOp(writer, "move");
            WritePath(writer, Path);
            WriteFromPath(writer, FromPath);

            writer.WriteEndObject();
        }

        /// <inheritdoc />
        public override void Read(JObject jOperation)
        {
            Path = new JsonPointer(SplitPath((string)jOperation.GetValue("path")));
            FromPath = new JsonPointer(SplitPath((string)jOperation.GetValue("from")));
        }
    }
}
