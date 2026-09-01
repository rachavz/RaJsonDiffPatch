//see https://github.com/tavis-software/Tavis.JsonPointer

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Tavis
{
    /// <summary>
    /// Represents a JSON Pointer as defined by RFC 6901, used to target specific values within a JSON document.
    /// </summary>
    public class JsonPointer
    {
        private static readonly string[] RootTokens = new string[0];

        /// <summary>
        /// The reference tokens, held in decoded form: <c>~1</c> and <c>~0</c> escapes have already been
        /// resolved, so each entry is the literal property name or array index it targets.
        /// </summary>
        private readonly IReadOnlyList<string> _tokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPointer"/> class from a pointer string.
        /// </summary>
        /// <param name="pointer">The JSON Pointer string (e.g. "/foo/bar/0"), or the empty string for the whole document.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pointer"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="pointer"/> is non-empty and does not start with '/'.</exception>
        public JsonPointer(string pointer)
        {
            if (pointer == null) throw new ArgumentNullException(nameof(pointer));
            if (pointer.Length == 0)
            {
                _tokens = RootTokens;
                return;
            }

            if (pointer[0] != '/')
                throw new ArgumentException(
                    "A JSON Pointer must either be empty or start with '/'; got '" + pointer + "'.", nameof(pointer));

            _tokens = pointer.Split('/').Skip(1).Select(Decode).ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPointer"/> class from already decoded reference tokens.
        /// </summary>
        /// <param name="decodedTokens">
        /// The reference tokens, with any <c>~1</c>/<c>~0</c> escapes already resolved.
        /// </param>
        internal JsonPointer(IReadOnlyList<string> decodedTokens)
        {
            _tokens = decodedTokens ?? RootTokens;
        }

        /// <summary>
        /// Decodes a reference token by unescaping <c>~1</c> to <c>/</c> and then <c>~0</c> to <c>~</c>.
        /// </summary>
        /// <param name="token">The encoded token.</param>
        /// <returns>The decoded token.</returns>
        /// <remarks>
        /// The order matters: unescaping <c>~0</c> first would turn <c>~01</c> into <c>~1</c> and then into <c>/</c>,
        /// losing the literal <c>~1</c> the pointer actually referred to.
        /// </remarks>
        private static string Decode(string token) => token.Replace("~1", "/").Replace("~0", "~");

        /// <summary>
        /// Encodes a reference token by escaping <c>~</c> as <c>~0</c> and then <c>/</c> as <c>~1</c>, per RFC 6901.
        /// </summary>
        /// <param name="token">The literal property name to encode.</param>
        /// <returns>The encoded token.</returns>
        public static string Encode(string token) => token.Replace("~", "~0").Replace("/", "~1");

        /// <summary>
        /// Gets the number of reference tokens in this pointer. Zero means the pointer targets the whole document.
        /// </summary>
        public int Count => _tokens.Count;

        /// <summary>
        /// Gets the last (decoded) reference token, or <c>null</c> if this pointer targets the whole document.
        /// </summary>
        public string LastToken => _tokens.Count == 0 ? null : _tokens[_tokens.Count - 1];

        /// <summary>
        /// Determines whether this pointer targets a new array element (i.e. its last token is "-").
        /// </summary>
        /// <returns><c>true</c> if the last token is "-"; <c>false</c> for the root pointer or any other token.</returns>
        public bool IsNewPointer()
        {
            return _tokens.Count != 0 && _tokens[_tokens.Count - 1] == "-";
        }

        /// <summary>
        /// Gets the parent pointer (this pointer with the last token removed), or <c>null</c> if this is a root pointer.
        /// </summary>
        public JsonPointer ParentPointer
        {
            get
            {
                if (_tokens.Count == 0) return null;

                var tokens = new string[_tokens.Count - 1];
                for (int i = 0; i < _tokens.Count - 1; i++)
                {
                    tokens[i] = _tokens[i];
                }

                return new JsonPointer(tokens);
            }
        }

        /// <summary>
        /// Navigates the JSON token tree using this pointer and returns the targeted token.
        /// </summary>
        /// <param name="sample">The root JSON token to search within.</param>
        /// <returns>The <see cref="JToken"/> at the location indicated by this pointer.</returns>
        /// <exception cref="ArgumentException">Thrown if the pointer cannot be resolved against <paramref name="sample"/>.</exception>
        public JToken Find(JToken sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            var current = sample;
            for (int i = 0; i < _tokens.Count; i++)
            {
                var token = _tokens[i];
                var array = current as JArray;
                if (array != null)
                {
                    int index;
                    if (!TryParseIndex(token, out index) || index >= array.Count)
                        throw Failure(i, "'" + token + "' is not a valid index into the array of " + array.Count + " element(s)");

                    current = array[index];
                    continue;
                }

                var obj = current as JObject;
                if (obj == null)
                    throw Failure(i, "the value there is a " + current.Type + ", not an object or an array");

                if (!obj.TryGetValue(token, out current))
                    throw Failure(i, "the object there has no '" + token + "' property");
            }

            return current;
        }

        /// <summary>
        /// Parses an array index, rejecting the signs, whitespace and leading zeroes that RFC 6901 disallows.
        /// </summary>
        private static bool TryParseIndex(string token, out int index)
        {
            index = 0;
            if (token.Length == 0) return false;
            if (token.Length > 1 && token[0] == '0') return false;

            return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index);
        }

        /// <summary>
        /// Builds a dereferencing failure that names the exact segment that could not be resolved.
        /// </summary>
        private ArgumentException Failure(int tokenIndex, string reason)
        {
            var resolved = new JsonPointer(_tokens.Take(tokenIndex).ToArray());
            return new ArgumentException(
                "Failed to dereference pointer '" + this + "' at segment " + (tokenIndex + 1) +
                " ('" + _tokens[tokenIndex] + "'): after '" + resolved + "', " + reason + ".");
        }

        /// <summary>
        /// Returns the string representation of this JSON Pointer (e.g. "/foo/bar/0"), re-escaping any
        /// <c>~</c> and <c>/</c> characters in the reference tokens. The root pointer renders as the empty string.
        /// </summary>
        /// <returns>The JSON Pointer string.</returns>
        public override string ToString()
        {
            if (_tokens.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < _tokens.Count; i++)
            {
                sb.Append('/').Append(Encode(_tokens[i]));
            }

            return sb.ToString();
        }
    }
}
