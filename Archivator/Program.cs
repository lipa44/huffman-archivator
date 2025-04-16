using System.Diagnostics;
using Archivator;

const string basePath = "./TestData/";
var files = Directory.EnumerateFiles(basePath);

var sw = new Stopwatch();
sw.Start();
foreach (var file in files.Where(x => x.Contains(".after") is false && x.Contains(".huffman") is false))
{
    var huffmanFile = file + ".huffman";

    var encoder = new HuffmanEncoder();
    encoder.Encode(file, huffmanFile);

    var decoder = new HuffmanDecoder();
    decoder.Decode(huffmanFile, file + ".after");

    File.Delete(huffmanFile);
}

var elapsed = sw.Elapsed;
Console.WriteLine($"Elapsed time: {elapsed.TotalMilliseconds}ms");
