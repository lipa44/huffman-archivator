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

        WriteArchive(outputPath, frequencyTable, inputData.Length, compressedData);

        var metrics = CalculateMetrics(inputData, frequencyTable, encodedBits.Length);
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

    private static void WriteArchive(
        string path,
        Dictionary<ushort, int> freq,
        int originalLength,
        byte[] compressedData
    )
    {
        using var writer = new BinaryWriter(File.Open(path, FileMode.Create));

        writer.Write(freq.Count);

        foreach (var (key, value) in freq)
        {
            writer.Write(key);
            writer.Write(value);
        }

        writer.Write(originalLength);
        writer.Write(compressedData.Length);
        writer.Write(compressedData);
    }

    private static CompressionMetrics CalculateMetrics(byte[] data, Dictionary<ushort, int> freq, int totalEncodedBits)
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

        foreach (var @byte in data)
        {
            singleByteFreq.TryAdd(@byte, 0);
            singleByteFreq[@byte]++;
        }

        var totalBytes = data.Length;
        var h1 = -singleByteFreq.Values.Sum(
            v =>
            {
                var p = (double) v / totalBytes;

                return p * Math.Log2(p);
            }
        );

        return new CompressionMetrics
        {
            H1 = h1,
            Hx = hx,
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
        Console.WriteLine(Row("Entropy H(X)", metrics.H1.ToString(floatFormat)));
        Console.WriteLine(Row("Entropy H(XX)", metrics.Hx.ToString(floatFormat)));
        Console.WriteLine(Row("Entropy H(X|X)", metrics.ConditionalEntropy.ToString(floatFormat)));
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
        public double H1 { get; init; }
        public double Hx { get; init; }
        public double ConditionalEntropy => Hx - H1;
        public double AvgBitsPerSymbol { get; init; }
        public int InitialSizeBytes { get; init; }
        public int CompressedSizeBytes { get; init; }
        public double CompressionRatioPercent => 100 - (CompressedSizeBytes / (float) InitialSizeBytes * 100);
    }
}
