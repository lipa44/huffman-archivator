namespace Archivator;

public class HuffmanDecoder
{
    public async Task Decode(string inputPath, string outputPath)
    {
        using var reader = new BinaryReader(File.OpenRead(inputPath));

        var metadata = ReadMetadata(reader);
        var compressedData = reader.ReadBytes(metadata.CompressedDataLength);

        var root = BuildHuffmanTree(metadata.FrequencyTable);
        var decoded = DecodeData(compressedData, root, metadata.OriginalByteLength);

        await File.WriteAllBytesAsync(outputPath, decoded);
    }

    private ArchiveMetadata ReadMetadata(BinaryReader reader)
    {
        var frequencyTable = ReadFrequencyTable(reader);
        var originalByteLength = reader.ReadInt32();
        var compressedDataLength = reader.ReadInt32();

        return new ArchiveMetadata
        {
            FrequencyTable = frequencyTable,
            OriginalByteLength = originalByteLength,
            CompressedDataLength = compressedDataLength
        };
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
            HuffmanNode left = pq.Dequeue(), right = pq.Dequeue();
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

    private byte[] DecodeData(byte[] compressedData, HuffmanNode root, int originalByteLength)
    {
        var output = new byte[originalByteLength];
        var current = root;
        var bytesWritten = 0;

        foreach (var b in compressedData)
        {
            for (var i = 7; i >= 0; i--)
            {
                var bit = (b & (1 << i)) != 0;
                current = bit ? current?.Right : current?.Left;

                if (current?.Symbol is not { } symbol) continue;

                var high = (byte) (symbol >> 8);
                var low = (byte) (symbol & 0xFF);

                if (bytesWritten < originalByteLength) output[bytesWritten++] = high;
                if (bytesWritten < originalByteLength) output[bytesWritten++] = low;

                current = root;

                if (bytesWritten >= originalByteLength) break;
            }
        }

        return output;
    }

    private record HuffmanNode
    {
        public HuffmanNode? Left { get; init; }
        public HuffmanNode? Right { get; init; }
        public int Frequency { get; init; }
        public ushort? Symbol { get; init; }
    }

    private record ArchiveMetadata
    {
        public Dictionary<ushort, int> FrequencyTable { get; init; }
        public int OriginalByteLength { get; init; }
        public int CompressedDataLength { get; init; }
    }
}
