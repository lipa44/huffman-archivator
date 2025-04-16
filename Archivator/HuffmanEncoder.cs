namespace Archivator;

using System.Text;

public class HuffmanEncoder
{
    private const int BlockSize = 2;

    public async Task Encode(string inputPath, string outputPath)
    {
        var inputData = await File.ReadAllBytesAsync(inputPath);

        // 1. Построение частотного словаря пар байтов
        var frequencyTable = BuildFrequencyTable(inputData);

        // 2. Построение дерева Хаффмана
        var root = BuildHuffmanTree(frequencyTable);
        var huffmanCodes = BuildHuffmanCodes(root);

        // 3. Сжатие данных
        var encodedData = EncodeData(inputData, huffmanCodes);
        var compressedData = ConvertBitStringToByteArray(encodedData);

        // 4. Сохраняем дерево + данные
        await using (var fs = new BinaryWriter(File.Open(outputPath, FileMode.Create)))
        {
            WriteFrequencyTable(fs, frequencyTable);
            fs.Write(compressedData.Length);
            fs.Write(compressedData);
        }

        // 5. Анализ
        AnalyzeEntropy(inputPath, inputData, frequencyTable, encodedData.Length);
    }

    private static Dictionary<ushort, int> BuildFrequencyTable(byte[] data)
    {
        var dict = new Dictionary<ushort, int>();

        for (var i = 0; i < data.Length - 1; i += BlockSize)
        {
            var pair = (ushort) ((data[i] << 8) | (i + 1 < data.Length ? data[i + 1] : 0));
            dict.TryAdd(pair, 0);
            dict[pair]++;
        }

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

        for (var i = 0; i < data.Length - 1; i += BlockSize)
        {
            var pair = (ushort) ((data[i] << 8) | (i + 1 < data.Length ? data[i + 1] : 0));
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

    private static void AnalyzeEntropy(
        string inputFile,
        byte[] data,
        Dictionary<ushort, int> freq,
        int totalEncodedBits
    )
    {
        var totalPairs = freq.Values.Sum();
        var Hx = -freq.Values.Sum(
            v =>
            {
                var p = (double) v / totalPairs;

                return p * Math.Log2(p);
            }
        );

        var singleByteFreq = new Dictionary<byte, int>();

        for (var i = 0; i < data.Length; i++)
        {
            if (!singleByteFreq.ContainsKey(data[i])) singleByteFreq[data[i]] = 0;
            singleByteFreq[data[i]]++;
        }

        var totalBytes = data.Length;
        var H1 = -singleByteFreq.Values.Sum(
            v =>
            {
                var p = (double) v / totalBytes;

                return p * Math.Log2(p);
            }
        );

        Console.WriteLine($"\nFile: {inputFile}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine($"{"Metric",-30}{"Value",25}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine($"{"Entropy H(X):",-30}{H1,25:F6}");
        Console.WriteLine($"{"Entropy H(XX):",-30}{Hx,25:F6}");
        Console.WriteLine($"{"Entropy H(X|X):",-30}{Hx - H1,25:F6}");
        Console.WriteLine($"{"Avg bits/symbol:",-30}{(double) totalEncodedBits / totalBytes,25:F6}");
        Console.WriteLine($"{"Initial size (bytes):",-30}{totalBytes,25}");
        Console.WriteLine($"{"Compressed size (bytes):",-30}{(totalEncodedBits + 7) / 8,25}");
        Console.WriteLine(new string('-', 55));
        Console.WriteLine(
            $"{"Compressed (%):",-30}{100 - ((totalEncodedBits + 7) / 8 / (float) totalBytes * 100),25:.00}"
        );
        Console.WriteLine(new string('-', 55));
    }

    public record HuffmanNode
    {
        public HuffmanNode? Left { get; init; }
        public HuffmanNode? Right { get; init; }
        public int Frequency { get; init; }
        public ushort? Symbol { get; init; }
    }
}
