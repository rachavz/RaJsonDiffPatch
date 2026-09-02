# RaJsonDiffPatch

A .NET Standard 2.0 implementation of JSON Patch ([RFC 6902](https://tools.ietf.org/html/rfc6902))
and JSON Pointer ([RFC 6901](https://tools.ietf.org/html/rfc6901)) for Json.NET (`JToken`) documents,
plus a diff generator that produces a patch from two documents.

```
dotnet add package RaJsonDiffPatch
```

## Namespaces

```csharp
using JsonDiffPatch;   // PatchDocument, JsonDiffer, JsonPatcher, the operations
using Tavis;           // JsonPointer
```

**Drop-in replacement for the original `JsonDiffPatch` package.** The namespaces and the public API
are unchanged, so swapping the package reference needs no source edits — you just get the fixes
below. Only the package id and assembly are renamed. Don't reference both packages at once: the
duplicated type names would be ambiguous.

### Fixed relative to the original

- `test` operations compared the found value against the whole document, so every `test` failed.
- JSON Pointer escaping was applied inconsistently — property names containing `/` or `~` produced
  invalid pointers, and a generated patch could not be re-parsed and re-applied.
- `Uri.UnescapeDataString` in the pointer decoder corrupted property names containing `%`.
- Root-level paths, out-of-range array indices, unknown `op` names and non-array patch documents
  raised `NullReferenceException` or silently did nothing; they now report what is wrong.
- `move` rejected `/a` -> `/ab` as "below from path" because the check was a string prefix test.
- A property named `""` (the empty string) was diffed at its parent's path instead of at `/`, so the
  patch removed or replaced the wrong value.
- The differ compared scalars by their text, so it never noticed a changed `byte[]`, ignored
  sub-second changes to dates, and reported `1.10m` -> `1.1` as a change. It now uses
  `JToken.DeepEquals`, like `test`.
- The differ allocated roughly a hundred bytes of garbage per byte of patch it produced. It now
  builds pointers from tokens instead of re-parsing strings and short-circuits unchanged subtrees;
  diffing is several times faster and allocates an order of magnitude less.

## Diffing two documents

```csharp
var left  = JToken.Parse(@"{ ""a"": 1, ""b"": [1, 2, 3] }");
var right = JToken.Parse(@"{ ""a"": 2, ""b"": [1, 2, 3, 4] }");

var patch = new JsonDiffer().Diff(left, right, useIdPropertyToDetermineEquality: false);

// [{"op":"replace","path":"/a","value":2},{"op":"add","path":"/b/3","value":4}]
Console.WriteLine(patch.ToString(Formatting.None));
```

Pass `useIdPropertyToDetermineEquality: true` to match objects inside arrays by their `id` property
rather than by deep equality. Reordered or edited elements are then diffed against the element with
the same `id`, instead of being reported as a wholesale replacement.

## Applying a patch

`Patch` takes the document by reference, because an operation on the root replaces it outright.

```csharp
JToken target = JToken.Parse(@"{ ""foo"": ""bar"" }");
var patch = PatchDocument.Parse(@"[{ ""op"": ""add"", ""path"": ""/baz"", ""value"": ""qux"" }]");

new JsonPatcher().Patch(ref target, patch);

// {"foo":"bar","baz":"qux"}
```

## Building a patch by hand

Operations are immutable; the path always comes first.

```csharp
var patch = new PatchDocument(
    new TestOperation(new JsonPointer("/a/b/c"), new JValue("foo")),
    new RemoveOperation(new JsonPointer("/a/b/c")),
    new AddOperation(new JsonPointer("/a/b/c"), new JArray(new JValue("foo"), new JValue("bar"))),
    new ReplaceOperation(new JsonPointer("/a/b/c"), new JValue(42)),
    new MoveOperation(new JsonPointer("/a/b/d"), new JsonPointer("/a/b/c")),
    new CopyOperation(new JsonPointer("/a/b/e"), new JsonPointer("/a/b/d")));
```

## Reading and writing the wire format

```csharp
var patch = PatchDocument.Parse(json);          // from a string
var patch = PatchDocument.Load(stream);         // from a stream (left open)
var patch = PatchDocument.Load(jArray);         // from an already-parsed JArray

string json  = patch.ToString(Formatting.None); // to a string
Stream bytes = patch.ToStream();                // to a new MemoryStream
patch.CopyToStream(existingStream);             // to a stream you own
```

## Pointers

`JsonPointer` handles RFC 6901 escaping, so property names containing `/` or `~` round-trip
correctly. Tokens are held decoded and re-escaped on `ToString()`.

```csharp
new JsonPointer("/a~1b").Find(doc);        // the property literally named "a/b"
new JsonPointer("/a~0b").Find(doc);        // the property literally named "a~b"
new JsonPointer("").Find(doc);             // the whole document
new JsonPointer("/").Find(doc);            // the property named ""
JsonPointer.Encode("a/b");                 // "a~1b"
```

A pointer that cannot be resolved throws `ArgumentException` naming the segment that failed:

> Failed to dereference pointer '/books/9/title' at segment 2 ('9'): after '/books', '9' is not a
> valid index into the array of 0 element(s).

## Behaviour worth knowing

- **`add` to a path holding an array appends to it** rather than replacing the array. RFC 6902
  specifies replacement; this library has always appended, and the behaviour is kept for
  compatibility. Target `/the/array/-` for an explicit append, or an index to insert.
- **The differ does not emit `move`, `copy` or `test`.** It produces `add`, `remove` and `replace`
  only. Array edits that change the length are expressed as a removal of the changed span followed
  by additions, so the patch is correct but not minimal.
- **`test` and the differ both compare with `JToken.DeepEquals`**: object member order is not
  significant, but numbers are compared by JSON type, so `1` does not equal `1.0` and the differ
  reports that as a `replace`.
- **`JsonPointer` has value equality.** Two pointers with the same tokens are equal and hash alike,
  so they can be dictionary keys. `IsSelfOrDescendantOf` compares whole tokens, not string prefixes.

## Targets

`netstandard2.0` — .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+, Mono, Xamarin, Unity.
The only dependency is `Newtonsoft.Json` 13.0.4.

## Credits and licence

Apache Licence 2.0 — see `License.txt`.

- Forked from [mcintyre321/JsonDiffPatch](https://github.com/mcintyre321/JsonDiffPatch)
- JSON Pointer from [tavis-software/Tavis.JsonPointer](https://github.com/tavis-software/Tavis.JsonPointer)
- Patch operations from [tavis-software/Tavis.JsonPatch](https://github.com/tavis-software/Tavis.JsonPatch)
- Diff generator by [Ian Mercer](http://blog.abodit.com/2014/05/json-patch-c-implementation/)
