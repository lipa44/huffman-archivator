namespace Archivator;

using System.Text;
using Humanizer;

public class HuffmanEncoder
{
    private const int BlockSize = 2;
    private const int BitsInByte = 8;
    private const byte FillerByte = 0x00;

    public async Task Encode(string inputPath, string outputPath)
    {
        var inputData = await File.ReadAllBytesAsync(inputPath);

        var frequencyTable = BuildFrequencyTable(inputData);
        var root = BuildHuffmanTree(frequencyTable);
        var huffmanCodes = BuildHuffmanCodes(root);

        var encodedBits = EncodeData(inputData, huffmanCodes);
        var compressedData = ConvertBitStringToByteArray(encodedBits);

        WriteEncodedFile(outputPath, frequencyTable, inputData.Length, compressedData);

        var metrics = CalculateMetrics(inputData, encodedBits.Length);
        PrintMetrics(inputPath, metrics);
    }

    private static Dictionary<ushort, int> BuildFrequencyTable(byte[] data)
    {
        var dict = new Dictionary<ushort, int>();

        int i;

        for (i = 0; i + 1 < data.Length; i += BlockSize)
        {
            var pair = (ushort) ((data[i] << BitsInByte) | data[i + 1]);
            dict.TryAdd(pair, 0);
            dict[pair]++;
        }

        if (i >= data.Length) return dict;

        // Обработка последнего байта (если нечётная длина)
        var lastPair = (ushort) ((data[i] << BitsInByte) | FillerByte); // добавляем фиктивный 0
        dict.TryAdd(lastPair, 0);
        dict[lastPair]++;

        return dict;
    }

    private static HuffmanNode BuildHuffmanTree(Dictionary<ushort, int> freq)
    {
        var pq = new PriorityQueue<HuffmanNode, int>();

        foreach (var (symbol, frequency) in freq)
        {
            pq.Enqueue(new HuffmanNode(symbol, frequency), frequency);
        }

        while (pq.Count > 1)
        {
            HuffmanNode left = pq.Dequeue(), right = pq.Dequeue();
            var parent = new HuffmanNode(null, left.Frequency + right.Frequency, left, right);

            pq.Enqueue(parent, left.Frequency + right.Frequency);
        }

        return pq.Dequeue();
    }

    private static Dictionary<ushort, string> BuildHuffmanCodes(HuffmanNode root)
    {
        var dict = new Dictionary<ushort, string>();
        var sb = new StringBuilder();

        void Traverse(HuffmanNode node)
        {
            if (node.Symbol.HasValue)
            {
                dict[node.Symbol.Value] = sb.ToString();

                return;
            }

            sb.Append('0');
            Traverse(node.Left!);
            sb.Length--;

            sb.Append('1');
            Traverse(node.Right!);
            sb.Length--;
        }

        Traverse(root);

        return dict;
    }

    private static string EncodeData(byte[] data, Dictionary<ushort, string> codes)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < data.Length; i += BlockSize)
        {
            var first = data[i];
            var second = i + 1 < data.Length ? data[i + 1] : FillerByte;

            var pair = (ushort) ((first << BitsInByte) | second);
            sb.Append(codes[pair]);
        }

        return sb.ToString();
    }

    private static byte[] ConvertBitStringToByteArray(string bits)
    {
        var numBytes = (bits.Length + 7) / BitsInByte;
        var result = new byte[numBytes];

        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] == '1')
            {
                result[i / BitsInByte] |= (byte) (1 << (BitsInByte - 1 - (i % BitsInByte)));
            }
        }

        return result;
    }

    private static void WriteEncodedFile(
        string outputPath,
        Dictionary<ushort, int> freq,
        int originalLength,
        byte[] compressedData
    )
    {
        using var writer = new BinaryWriter(File.Open(outputPath, FileMode.Create));

        writer.Write(freq.Count);

        foreach (var (key, value) in freq)
        {
            writer.Write(key);
            writer.Write(value);
        }

        writer.Write(originalLength);
        writer.Write(compressedData.Length);
        writer.Write(compressedData);

        Console.WriteLine($"Encoded file written to: {outputPath}");
    }

    private static CompressionMetrics CalculateMetrics(byte[] data, int totalEncodedBits)
    {
        const int BitsInByte = 8;

        var singleByteFreq = new Dictionary<byte, int>();
        var pairFreq = new Dictionary<byte, Dictionary<byte, int>>();
        var tripleFreq = new Dictionary<(byte, byte), Dictionary<byte, int>>();

        for (var i = 0; i < data.Length; i++)
        {
            var current = data[i];
            singleByteFreq.TryAdd(current, 0);
            singleByteFreq[current]++;

            if (i >= 1)
            {
                var prev = data[i - 1];
                if (!pairFreq.ContainsKey(prev))
                    pairFreq[prev] = new Dictionary<byte, int>();

                var dict = pairFreq[prev];
                dict.TryAdd(current, 0);
                dict[current]++;
            }

            if (i >= 2)
            {
                var prev1 = data[i - 2];
                var prev2 = data[i - 1];
                var key = (prev1, prev2);

                if (!tripleFreq.ContainsKey(key))
                    tripleFreq[key] = new Dictionary<byte, int>();

                var dict = tripleFreq[key];
                dict.TryAdd(current, 0);
                dict[current]++;
            }
        }

        var totalBytes = data.Length;
        var totalPairs = totalBytes - 1;
        var totalTriples = totalBytes - 2;

        // HX
        var h1 = -singleByteFreq.Values.Sum(
            v =>
            {
                var p = (double) v / totalBytes;

                return p * Math.Log2(p);
            }
        );

        // H(X|X)
        double hXgivenX = 0;

        foreach (var kvp in pairFreq)
        {
            var px = (double) kvp.Value.Values.Sum() / totalPairs;
            double h = 0;

            foreach (var count in kvp.Value.Values)
            {
                var p = (double) count / kvp.Value.Values.Sum();
                h += -p * Math.Log2(p);
            }

            hXgivenX += px * h;
        }

        // H(X|XX)
        double hXgivenXX = 0;

        foreach (var kvp in tripleFreq)
        {
            var pxx = (double) kvp.Value.Values.Sum() / totalTriples;
            double h = 0;

            foreach (var count in kvp.Value.Values)
            {
                var p = (double) count / kvp.Value.Values.Sum();
                h += -p * Math.Log2(p);
            }

            hXgivenXX += pxx * h;
        }

        return new CompressionMetrics
        {
            HX = h1,
            HX_X = hXgivenX,
            HX_XX = hXgivenXX,
            AvgBitsPerSymbol = (double) totalEncodedBits / totalBytes,
            InitialSizeBytes = totalBytes,
            CompressedSizeBytes = (totalEncodedBits + 7) / BitsInByte
        };
    }

    private static void PrintMetrics(string inputFile, CompressionMetrics metrics)
    {
        const int leftOffset = 16;
        const int rightOffset = 11;
        const string floatFormat = "F3";

        string Line(char left, char mid, char right, char fill) =>
            $"{left}{new string(fill, leftOffset + 1)}{mid}{new string(fill, rightOffset + 1)}{right}";

        string Row(string key, string value) => $"│ {key,-leftOffset}│{value,rightOffset} │";

        Console.WriteLine($"\nFile: {inputFile}");
        Console.WriteLine(Line('┌', '┬', '┐', '─'));
        Console.WriteLine(Row("Entropy H(X)", metrics.HX.ToString(floatFormat)));
        Console.WriteLine(Row("Entropy H(X|X)", metrics.HX_X.ToString(floatFormat)));
        Console.WriteLine(Row("Entropy H(X|XX)", metrics.HX_XX.ToString(floatFormat)));
        Console.WriteLine(Row("Avg bits/symbol", metrics.AvgBitsPerSymbol.ToString(floatFormat)));
        Console.WriteLine(Line('├', '┼', '┤', '─'));
        Console.WriteLine(Row("Initial size", metrics.InitialSizeBytes.Bytes().ToString()));
        Console.WriteLine(Row("Compressed size", metrics.CompressedSizeBytes.Bytes().ToString()));
        Console.WriteLine(Line('├', '┼', '┤', '─'));
        Console.WriteLine(Row("Compressed (%)", metrics.CompressionRatioPercent.ToString(floatFormat)));
        Console.WriteLine(Line('└', '┴', '┘', '─'));
    }

    public record HuffmanNode(ushort? Symbol, int Frequency, HuffmanNode? Left = null, HuffmanNode? Right = null);

    private record CompressionMetrics
    {
        public double HX { get; init; }
        public double HX_X { get; init; }
        public double HX_XX { get; init; }
        public double AvgBitsPerSymbol { get; init; }
        public int InitialSizeBytes { get; init; }
        public int CompressedSizeBytes { get; init; }
        public double CompressionRatioPercent => 100 - (CompressedSizeBytes / (float) InitialSizeBytes * 100);
    }
}
