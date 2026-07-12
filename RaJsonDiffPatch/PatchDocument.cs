using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RaJsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch document as defined by RFC 6902, containing an ordered list of operations.
    /// </summary>
    public class PatchDocument
    {
        private readonly List<Operation> _Operations = new List<Operation>();

        /// <summary>
        /// Initializes a new instance of the <see cref="PatchDocument"/> class with the specified operations.
        /// </summary>
        /// <param name="operations">The operations to include in this patch document.</param>
        public PatchDocument(params Operation[] operations)
        {
            _Operations.AddRange(operations);
        }

        /// <summary>
        /// Gets the collection of operations in this patch document.
        /// </summary>
        public IReadOnlyCollection<Operation> Operations
        {
            get { return _Operations; }
        }

        /// <summary>
        /// Adds an operation to this patch document.
        /// </summary>
        /// <param name="operation">The operation to add.</param>
        public void AddOperation(Operation operation)
        {
            _Operations.Add(operation);
        }

        /// <summary>
        /// Loads a <see cref="PatchDocument"/> from a stream containing a JSON Patch array.
        /// </summary>
        /// <param name="document">The stream containing the JSON Patch document.</param>
        /// <returns>The loaded <see cref="PatchDocument"/>.</returns>
        public static PatchDocument Load(Stream document)
        {
            var reader = new StreamReader(document);
       
            return Parse(reader.ReadToEnd());
        }

        /// <summary>
        /// Loads a <see cref="PatchDocument"/> from a <see cref="JArray"/>.
        /// </summary>
        /// <param name="document">The JSON array representing the patch operations.</param>
        /// <returns>The loaded <see cref="PatchDocument"/>.</returns>
        public static PatchDocument Load(JArray document)
        {
            var root = new PatchDocument();

            if (document != null)
            {
                foreach (var jOperation in document.Children().Cast<JObject>())
                {
                    var op = Operation.Build(jOperation);
                    root.AddOperation(op);
                }
            }
            
            return root;
        }
        
        /// <summary>
        /// Parses a JSON string into a <see cref="PatchDocument"/>.
        /// </summary>
        /// <param name="jsondocument">A JSON string representing a patch document (an array of operations).</param>
        /// <returns>The parsed <see cref="PatchDocument"/>.</returns>
        public static PatchDocument Parse(string jsondocument)
        {
            var root = JToken.Parse(jsondocument) as JArray;
            
            return Load(root);
        }

        /// <summary>
        /// Creates an <see cref="Operation"/> instance based on the operation name.
        /// </summary>
        /// <param name="op">The operation name (e.g. "add", "remove", "replace", "move", "copy", "test").</param>
        /// <returns>A new operation instance, or <c>null</c> if the operation name is not recognized.</returns>
        public static Operation CreateOperation(string op)
        {
            switch (op)
            {
                case "add": return new AddOperation();
                case "copy": return new CopyOperation();
                case "move": return new MoveOperation();
                case "remove": return new RemoveOperation();
                case "replace": return new ReplaceOperation();
                case "test" : return new TestOperation();
            }
            return null;
        }

        /// <summary>
        /// Creates a memory stream with the serialized version of this <see cref="PatchDocument"/>.
        /// </summary>
        /// <returns>A <see cref="MemoryStream"/> containing the JSON Patch document.</returns>
        public MemoryStream ToStream()
        {
            var stream = new MemoryStream();
            CopyToStream(stream, Formatting.Indented);
            stream.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Writes the serialized patch document to the provided stream.
        /// </summary>
        /// <param name="stream">The target stream to write to.</param>
        /// <param name="formatting">The JSON formatting to apply. Defaults to <see cref="Formatting.Indented"/>.</param>
        public void CopyToStream(Stream stream, Formatting formatting = Formatting.Indented)
        {
            var sw = new JsonTextWriter(new StreamWriter(stream));
            sw.Formatting = formatting;

            sw.WriteStartArray();

            foreach (var operation in Operations)
            {
                operation.Write(sw);
            }
            
            sw.WriteEndArray();

            sw.Flush();
        }

        /// <summary>
        /// Returns the JSON Patch document as an indented JSON string.
        /// </summary>
        /// <returns>The JSON string representation of this patch document.</returns>
        public override string ToString()
        {
            return ToString(Formatting.Indented);
        }

        /// <summary>
        /// Returns the JSON Patch document as a JSON string with the specified formatting.
        /// </summary>
        /// <param name="formatting">The JSON formatting to apply.</param>
        /// <returns>The JSON string representation of this patch document.</returns>
        public string ToString(Formatting formatting)
        {
            using (var ms = new MemoryStream())
            {
                CopyToStream(ms, formatting);
                ms.Position = 0;
                using (StreamReader reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
