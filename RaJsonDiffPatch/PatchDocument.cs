using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonDiffPatch
{
    /// <summary>
    /// Represents a JSON Patch document as defined by RFC 6902, containing an ordered list of operations.
    /// </summary>
    public class PatchDocument
    {
        private readonly List<Operation> _operations = new List<Operation>();
        private readonly ReadOnlyCollection<Operation> _readOnlyOperations;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatchDocument"/> class with the specified operations.
        /// </summary>
        /// <param name="operations">The operations to include in this patch document.</param>
        public PatchDocument(params Operation[] operations)
        {
            if (operations != null) _operations.AddRange(operations);
            _readOnlyOperations = _operations.AsReadOnly();
        }

        /// <summary>
        /// Gets the collection of operations in this patch document.
        /// </summary>
        public IReadOnlyCollection<Operation> Operations
        {
            get { return _readOnlyOperations; }
        }

        /// <summary>
        /// Adds an operation to this patch document.
        /// </summary>
        /// <param name="operation">The operation to add.</param>
        public void AddOperation(Operation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            _operations.Add(operation);
        }

        /// <summary>
        /// Loads a <see cref="PatchDocument"/> from a stream containing a JSON Patch array.
        /// </summary>
        /// <param name="document">The stream containing the JSON Patch document.</param>
        /// <returns>The loaded <see cref="PatchDocument"/>.</returns>
        public static PatchDocument Load(Stream document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            // Leave the caller's stream open: it owns it, and callers pass shared streams
            // such as Assembly.GetManifestResourceStream.
            using (var reader = new StreamReader(document, Encoding.UTF8, true, 1024, true))
            {
                return Parse(reader.ReadToEnd());
            }
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
                for (int i = 0; i < document.Count; i++)
                {
                    var jOperation = document[i] as JObject;
                    if (jOperation == null)
                        throw new ArgumentException(
                            "Entry " + i + " of a JSON Patch document must be an object, but is a " +
                            document[i].Type + ".", nameof(document));

                    root.AddOperation(Operation.Build(jOperation));
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
            if (root == null)
                throw new ArgumentException(
                    "A JSON Patch document must be a JSON array of operations.", nameof(jsondocument));

            return Load(root);
        }

        /// <summary>
        /// Creates an <see cref="Operation"/> instance based on the operation name.
        /// </summary>
        /// <param name="op">The operation name (e.g. "add", "remove", "replace", "move", "copy", "test").</param>
        /// <returns>A new operation instance.</returns>
        /// <exception cref="ArgumentException">The operation name is not one of the six defined by RFC 6902.</exception>
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

            throw new ArgumentException(
                op == null
                    ? "A JSON Patch operation requires an 'op' member."
                    : "'" + op + "' is not a JSON Patch operation; expected add, copy, move, remove, replace or test.",
                nameof(op));
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
            // Deliberately neither disposed nor closed: disposing the writer would close the
            // caller's stream, and ToStream/ToString still need to read back from it.
            var writer = new JsonTextWriter(new StreamWriter(stream, new UTF8Encoding(false), 1024, true));
            writer.Formatting = formatting;
            Write(writer);
            writer.Flush();
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
            var sw = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new JsonTextWriter(sw) { Formatting = formatting })
            {
                Write(writer);
            }

            return sw.ToString();
        }

        private void Write(JsonWriter writer)
        {
            writer.WriteStartArray();

            foreach (var operation in _operations)
            {
                operation.Write(writer);
            }

            writer.WriteEndArray();
        }
    }
}
