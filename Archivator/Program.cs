using System.Diagnostics;
using Archivator;

const string basePath = "./../../../TestData/";
var files = Directory.EnumerateFiles(basePath);

var sw = new Stopwatch();
sw.Start();

await Parallel.ForEachAsync(
    files.Where(x => x.Contains(".after") is false && x.Contains(".huffman") is false),
    async (file, _) =>
    {
        var huffmanFile = file + ".huffman";

        var encoder = new HuffmanEncoder();
        await encoder.Encode(file, huffmanFile);

        var decoder = new HuffmanDecoder();
        await decoder.Decode(huffmanFile, file + ".after");

        File.Delete(huffmanFile);
    }
);

var elapsed = sw.Elapsed;
Console.WriteLine($"Total elapsed time: {elapsed.TotalMilliseconds}ms");
