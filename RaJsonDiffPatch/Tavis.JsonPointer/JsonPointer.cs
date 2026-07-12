//see https://github.com/tavis-software/Tavis.JsonPointer

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Tavis
{
    /// <summary>
    /// Represents a JSON Pointer as defined by RFC 6901, used to target specific values within a JSON document.
    /// </summary>
    public class JsonPointer
    {
        private readonly IReadOnlyList<string> _Tokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPointer"/> class from a pointer string.
        /// </summary>
        /// <param name="pointer">The JSON Pointer string (e.g. "/foo/bar/0").</param>
        public JsonPointer(string pointer)
        {
            _Tokens = pointer.Split('/').Skip(1).Select(Decode).ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPointer"/> class from pre-parsed tokens.
        /// </summary>
        /// <param name="tokens">The parsed pointer tokens.</param>
        internal JsonPointer(IReadOnlyList<string> tokens)
        {
            _Tokens = tokens;
        }
        /// <summary>
        /// Decodes a JSON Pointer token by unescaping <c>~1</c> to <c>/</c> and <c>~0</c> to <c>~</c>.
        /// </summary>
        /// <param name="token">The encoded token.</param>
        /// <returns>The decoded token.</returns>
        private string Decode(string token)
        {
            return Uri.UnescapeDataString(token).Replace("~1", "/").Replace("~0", "~");
        }

        /// <summary>
        /// Determines whether this pointer targets a new array element (i.e. ends with "-").
        /// </summary>
        /// <returns><c>true</c> if the pointer ends with "-"; otherwise <c>false</c>.</returns>
        public bool IsNewPointer()
        {
            return _Tokens[_Tokens.Count - 1] == "-";
        }

        /// <summary>
        /// Gets the parent pointer (this pointer with the last token removed), or <c>null</c> if this is a root pointer.
        /// </summary>
        public JsonPointer ParentPointer
        {
            get
            {
                if (_Tokens.Count == 0) return null;

                var tokens = new string[_Tokens.Count - 1];
                for (int i = 0; i < _Tokens.Count - 1; i++)
                {
                    tokens[i] = _Tokens[i];
                }

                return new JsonPointer(tokens);
            }
        }

        /// <summary>
        /// Navigates the JSON token tree using this pointer and returns the targeted token.
        /// </summary>
        /// <param name="sample">The root JSON token to search within.</param>
        /// <returns>The <see cref="JToken"/> at the location indicated by this pointer.</returns>
        /// <exception cref="ArgumentException">Thrown if the pointer cannot be resolved.</exception>
        public JToken Find(JToken sample)
        {
            if (_Tokens.Count == 0)
            {
                return sample;
            }
            try
            {
                var pointer = sample;
                foreach (var token in _Tokens.Select(t => t.Replace("~1", "/").Replace("~0", "~")))
                {
                    if (pointer is JArray)
                    {
                        pointer = pointer[Convert.ToInt32(token)];
                    }
                    else
                    {
                        pointer = pointer[token];
                        if (pointer == null)
                        {
                            throw new ArgumentException("Cannot find " + token);
                        }

                    }
                }
                return pointer;
            }
            catch (Exception ex)
            {
                throw  new ArgumentException("Failed to dereference pointer",ex);
            }
        }

        /// <summary>
        /// Returns the string representation of this JSON Pointer (e.g. "/foo/bar/0").
        /// </summary>
        /// <returns>The JSON Pointer string.</returns>
        public override string ToString()
        {
            return "/" + String.Join("/", _Tokens);
        }
    }
}
