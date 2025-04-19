namespace Archivator;

using System.Text;

public class HuffmanEncoder
{
    private const int BlockSize = 2;

    public async Task Encode(string inputPath, string outputPath)
    {
        var inputData = await File.ReadAllBytesAsync(inputPath);

        var frequencyTable = BuildFrequencyTable(inputData);
        var root = BuildHuffmanTree(frequencyTable);
        var huffmanCodes = BuildHuffmanCodes(root);

        var encodedData = EncodeData(inputData, huffmanCodes);
        var compressedData = ConvertBitStringToByteArray(encodedData);

        await using (var fs = new BinaryWriter(File.Open(outputPath, FileMode.Create)))
        {
            WriteFrequencyTable(fs, frequencyTable);
            fs.Write(inputData.Length);
            fs.Write(compressedData.Length);
            fs.Write(compressedData);
        }

        var metrics = CalcCompressionMetrics(inputData, frequencyTable, encodedData.Length);
        PrintCompressionMetrics(inputPath, metrics);
    }

    private static Dictionary<ushort, int> BuildFrequencyTable(byte[] data)
    {
        var dict = new Dictionary<ushort, int>();

        int i;

        for (i = 0; i + 1 < data.Length; i += BlockSize)
        {
            var pair = (ushort) ((data[i] << 8) | data[i + 1]);
            dict.TryAdd(pair, 0);
            dict[pair]++;
        }

        if (i >= data.Length) return dict;

        // Обработка последнего байта (если нечётная длина)
        var lastPair = (ushort) ((data[i] << 8) | 0x00); // добавляем фиктивный 0
        dict.TryAdd(lastPair, 0);
        dict[lastPair]++;

        return dict;
    }

    private static HuffmanNode BuildHuffmanTree(Dictionary<ushort, int> freq)
    {
        var pq = new PriorityQueue<HuffmanNode, int>();

        foreach (var kvp in freq)
        {
            pq.Enqueue(new HuffmanNode { Symbol = kvp.Key, Frequency = kvp.Value }, kvp.Value);
        }

        while (pq.Count > 1)
        {
            var left = pq.Dequeue();
            var right = pq.Dequeue();
            var parent = new HuffmanNode
            {
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };
            pq.Enqueue(parent, parent.Frequency);
        }

        return pq.Dequeue();
    }

    private static Dictionary<ushort, string> BuildHuffmanCodes(HuffmanNode root)
    {
        var dict = new Dictionary<ushort, string>();

        void Traverse(HuffmanNode node, string code)
        {
            if (node.Symbol.HasValue)
            {
                dict[node.Symbol.Value] = code;
            }
            else
            {
                Traverse(node.Left, code + "0");
                Traverse(node.Right, code + "1");
            }
        }

        Traverse(root, string.Empty);

        return dict;
    }

    private static string EncodeData(byte[] data, Dictionary<ushort, string> codes)
    {
        StringBuilder sb = new();

        for (var i = 0; i < data.Length; i += BlockSize)
        {
            var first = data[i];
            var second = i + 1 < data.Length ? data[i + 1] : (byte) 0;

            var pair = (ushort) ((first << 8) | second);
            sb.Append(codes[pair]);
        }

        return sb.ToString();
    }

    private static byte[] ConvertBitStringToByteArray(string bits)
    {
        var numBytes = (bits.Length + 7) / 8;
        var result = new byte[numBytes];

        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] == '1')
            {
                result[i / 8] |= (byte) (1 << (7 - (i % 8)));
            }
        }

        return result;
    }

    private static void WriteFrequencyTable(BinaryWriter writer, Dictionary<ushort, int> freq)
    {
        writer.Write(freq.Count);

        foreach (var kvp in freq)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }
    }

    private static CompressionMetrics CalcCompressionMetrics(
        byte[] data,
        Dictionary<ushort, int> freq,
        int totalEncodedBits
    )
    {
        var totalPairs = freq.Values.Sum();
        var hx = -freq.Values.Sum(
            v =>
            {
                var p = (double) v / totalPairs;

                return p * Math.Log2(p);
            }
        );

        var singleByteFreq = new Dictionary<byte, int>();

        foreach (var t in data)
        {
            singleByteFreq.TryAdd(t, 0);
            singleByteFreq[t]++;
        }

        var totalBytes = data.Length;
        var h1 = -singleByteFreq.Values.Sum(
            v =>
            {
                var p = (double) v / totalBytes;

                return p * Math.Log2(p);
            }
        );

        var metrics = new CompressionMetrics
        {
            H1 = h1,
            Hx = hx,
            AvgBitsPerSymbol = (double) totalEncodedBits / totalBytes,
            InitialSizeBytes = totalBytes,
            CompressedSizeBytes = (totalEncodedBits + 7) / 8
        };

        return metrics;
    }

    private static void PrintCompressionMetrics(string inputFile, CompressionMetrics metrics)
    {
        Console.WriteLine($"\nFile: {inputFile}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine($"{"Metric",-30}{"Value",25}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine($"{"Entropy H(X):",-30}{metrics.H1,25:F6}");
        Console.WriteLine($"{"Entropy H(XX):",-30}{metrics.Hx,25:F6}");
        Console.WriteLine($"{"Entropy H(X|X):",-30}{metrics.ConditionalEntropy,25:F6}");
        Console.WriteLine($"{"Avg bits/symbol:",-30}{metrics.AvgBitsPerSymbol,25:F6}");
        Console.WriteLine($"{"Initial size (bytes):",-30}{metrics.InitialSizeBytes,25}");
        Console.WriteLine($"{"Compressed size (bytes):",-30}{metrics.CompressedSizeBytes,25}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine($"{"Compressed (%):",-30}{metrics.CompressionRatioPercent,25:.00}");
        Console.WriteLine(new string('-', 55));
    }

    public record HuffmanNode
    {
        public HuffmanNode? Left { get; init; }
        public HuffmanNode? Right { get; init; }
        public int Frequency { get; init; }
        public ushort? Symbol { get; init; }
    }

    private record CompressionMetrics
    {
        public double H1 { get; init; }
        public double Hx { get; init; }
        public double ConditionalEntropy => Hx - H1;
        public double AvgBitsPerSymbol { get; init; }
        public int InitialSizeBytes { get; init; }
        public int CompressedSizeBytes { get; init; }
        public double CompressionRatioPercent => 100 - (CompressedSizeBytes / (float) InitialSizeBytes * 100);
    }
}
