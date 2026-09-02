using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonDiffPatch.Benchmark
{
    /// <summary>
    /// Times the four operations a consumer pays for: diff, patch, serialize and parse. Usage:
    /// <c>dotnet run -c Release -- [records] [iterations]</c>. The SHA-256 of the generated patch is
    /// printed so two builds can be checked for identical output, not just similar speed.
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            int records = args.Length > 0 ? int.Parse(args[0]) : 2000;
            int iterations = args.Length > 1 ? int.Parse(args[1]) : 20;

            var from = MakeDocument(1, records);
            var to = Mutate(from, 10);
            var differ = new JsonDiffer();
            var patcher = new JsonPatcher();

            for (int i = 0; i < 3; i++)
            {
                differ.Diff(from, to, false).ToString(Formatting.None);
            }

            Measure("diff (deep equality)", iterations, () => differ.Diff(from, to, false));
            Measure("diff (id equality)  ", iterations, () => differ.Diff(from, to, true));

            var patch = differ.Diff(from, to, false);
            var json = patch.ToString(Formatting.None);
            var parsed = JToken.Parse(json);

            var targets = new JToken[iterations];
            for (int i = 0; i < iterations; i++) targets[i] = from.DeepClone();
            int next = 0;
            Measure("patch apply         ", iterations, () => { var t = targets[next++]; patcher.Patch(ref t, patch); });

            Measure("patch ToString      ", iterations, () => patch.ToString(Formatting.None));
            Measure("patch Parse         ", iterations, () => PatchDocument.Parse(json));
            Measure("  JToken.Parse only ", iterations, () => JToken.Parse(json));
            Measure("  Load(JArray) only ", iterations, () => PatchDocument.Load((JArray)parsed));

            var check = from.DeepClone();
            patcher.Patch(ref check, patch);
            Console.WriteLine();
            Console.WriteLine("operations : " + patch.Operations.Count);
            Console.WriteLine("patch bytes: " + json.Length);
            Console.WriteLine("roundtrip  : " + (JToken.DeepEquals(check, to) ? "ok" : "FAILED"));
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                Console.WriteLine("sha256     : " + BitConverter.ToString(hash, 0, 8).Replace("-", ""));
            }
        }

        private static void Measure(string name, int iterations, Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            stopwatch.Stop();

            double allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations / 1024;
            Console.WriteLine($"{name}  {stopwatch.Elapsed.TotalMilliseconds / iterations,9:F3} ms/op  {allocated,10:F1} KB/op");
        }

        /// <summary>
        /// A list of records with nested objects, arrays and keys that need pointer escaping.
        /// </summary>
        private static JObject MakeDocument(int seed, int records)
        {
            var random = new Random(seed);
            var items = new JArray();
            for (int i = 0; i < records; i++)
            {
                items.Add(new JObject
                {
                    ["id"] = i,
                    ["name"] = "item " + i,
                    ["price"] = random.NextDouble() * 100,
                    ["tags"] = new JArray("a", "b", "c/" + i % 7, "d~" + i % 5),
                    ["nested"] = new JObject
                    {
                        ["x"] = i * 2,
                        ["y"] = "yy",
                        ["deep"] = new JObject { ["k1"] = true, ["k2"] = null, ["k3"] = 1.5 }
                    }
                });
            }

            return new JObject
            {
                ["version"] = 1,
                ["items"] = items,
                ["meta"] = new JObject { ["owner"] = "ra", ["flags"] = new JArray(1, 2, 3) }
            };
        }

        /// <summary>
        /// Scattered edits plus one insertion and one removal, which shifts half the array: the
        /// differ's worst case, because it has no LCS.
        /// </summary>
        private static JObject Mutate(JObject document, int every)
        {
            var copy = (JObject)document.DeepClone();
            var items = (JArray)copy["items"];
            for (int i = 0; i < items.Count; i++)
            {
                if (i % every == 0) items[i]["price"] = 42;
                if (i % (every * 3) == 0) items[i]["nested"]["deep"]["k4"] = "new";
                if (i % (every * 5) == 0) ((JObject)items[i]["nested"]).Remove("y");
            }

            items.RemoveAt(items.Count / 2);
            items.Insert(3, new JObject { ["id"] = 999999, ["name"] = "inserted" });
            copy["version"] = 2;
            return copy;
        }
    }
}
