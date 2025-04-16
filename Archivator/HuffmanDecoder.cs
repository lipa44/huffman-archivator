namespace Archivator;

public class HuffmanDecoder
{
    public async Task Decode(string inputPath, string outputPath)
    {
        using var reader = new BinaryReader(File.OpenRead(inputPath));
        var frequencyTable = ReadFrequencyTable(reader);
        var compressedDataLength = reader.ReadInt32();
        var compressedData = reader.ReadBytes(compressedDataLength);

        var root = BuildHuffmanTree(frequencyTable);
        var decoded = DecodeData(compressedData, root, frequencyTable);
        await File.WriteAllBytesAsync(outputPath, decoded);
    }

    private Dictionary<ushort, int> ReadFrequencyTable(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var table = new Dictionary<ushort, int>();

        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadUInt16();
            var freq = reader.ReadInt32();
            table[key] = freq;
        }

        return table;
    }

    private HuffmanNode BuildHuffmanTree(Dictionary<ushort, int> freq)
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
                Left = left,
                Right = right,
                Frequency = left.Frequency + right.Frequency
            };
            pq.Enqueue(parent, parent.Frequency);
        }

        return pq.Dequeue();
    }

    private byte[] DecodeData(byte[] compressedData, HuffmanNode root, Dictionary<ushort, int> freq)
    {
        List<byte> output = new();
        var current = root;
        var totalSymbols = freq.Values.Sum();
        var symbolsDecoded = 0;

        foreach (var b in compressedData)
        {
            for (var i = 7; i >= 0; i--)
            {
                var bit = (b & (1 << i)) != 0;
                current = bit ? current?.Right : current?.Left;

                if (current?.Symbol is not { } symbol) { continue; }

                var pair = symbol;
                var high = (byte) (pair >> 8);
                var low = (byte) (pair & 0xFF);
                output.Add(high);
                output.Add(low);
                current = root;
                symbolsDecoded++;

                if (symbolsDecoded >= totalSymbols)
                    return output.ToArray();
            }
        }

        return output.ToArray();
    }

    private record HuffmanNode
    {
        public HuffmanNode? Left { get; init; }
        public HuffmanNode? Right { get; init; }
        public int Frequency { get; init; }
        public ushort? Symbol { get; init; }
    }
}
