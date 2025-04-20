namespace Archivator;

public class HuffmanDecoder
{
    private const int BitsInByte = 8;
    private const byte ByteMask = 0xFF;

    public async Task Decode(string inputPath, string outputPath)
    {
        var fileExists = File.Exists(inputPath);

        if (fileExists is false)
        {
            Console.WriteLine($"Файл '{inputPath}' не удалось найти для декодирования");
            return;
        }

        using var reader = new BinaryReader(File.OpenRead(inputPath));

        var metadata = ReadMetadata(reader);
        var compressedData = reader.ReadBytes(metadata.CompressedDataLength);

        var root = BuildHuffmanTree(metadata.FrequencyTable);
        var decoded = DecodeData(compressedData, root, metadata.OriginalByteLength);

        await WriteDecodedFile(outputPath, decoded);
    }

    private static async Task WriteDecodedFile(string outputPath, byte[] decoded)
    {
        await File.WriteAllBytesAsync(outputPath, decoded);
        Console.WriteLine($"Decoded file written to: {outputPath}");
    }

    private ArchiveMetadata ReadMetadata(BinaryReader reader)
    {
        var frequencyTable = ReadFrequencyTable(reader);
        var originalByteLength = reader.ReadInt32();
        var compressedDataLength = reader.ReadInt32();

        return new ArchiveMetadata(frequencyTable, originalByteLength, compressedDataLength);
    }

    private Dictionary<ushort, int> ReadFrequencyTable(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var table = new Dictionary<ushort, int>(count);

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

        foreach (var (symbol, frequency) in freq)
        {
            pq.Enqueue(new HuffmanNode(symbol, frequency), frequency);
        }

        while (pq.Count > 1)
        {
            HuffmanNode left = pq.Dequeue(), right = pq.Dequeue();
            var parent = new HuffmanNode(null, left.Frequency + right.Frequency, left, right);

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
            for (var i = BitsInByte - 1; i >= 0; i--)
            {
                var bit = (b & (1 << i)) != 0;
                current = bit ? current?.Right : current?.Left;

                if (current?.Symbol is not { } symbol) continue;

                WriteSymbolBytes(output, symbol, ref bytesWritten, originalByteLength);
                current = root;

                if (bytesWritten >= originalByteLength) return output;
            }
        }

        return output;
    }

    private static void WriteSymbolBytes(byte[] output, ushort symbol, ref int offset, int limit)
    {
        var high = (byte) (symbol >> BitsInByte);
        var low = (byte) (symbol & ByteMask);

        if (offset < limit) output[offset++] = high;
        if (offset < limit) output[offset++] = low;
    }

    private record HuffmanNode(ushort? Symbol, int Frequency, HuffmanNode? Left = null, HuffmanNode? Right = null);

    private record ArchiveMetadata(
        Dictionary<ushort, int> FrequencyTable,
        int OriginalByteLength,
        int CompressedDataLength
    );
}
