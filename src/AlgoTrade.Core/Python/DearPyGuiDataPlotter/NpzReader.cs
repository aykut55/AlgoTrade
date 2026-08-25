using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>
/// <see cref="NpzWriter"/>'ın ürettiği (numpy np.load(path) ile de okunabilen) .npz
/// dosyalarını C# tarafında geri okur. NpzWriter'daki format varsayımlarının
/// (.npy v1.0, C-order/row-major, '&lt;f8'/'&lt;i8'/'&lt;U{n}' dtype'ları) simetriğidir —
/// bkz. NpzWriter.cs. Harici bir numpy/Python bağımlılığı gerektirmez.
/// </summary>
public class NpzReader
{
    private readonly Dictionary<string, byte[]> _rawEntries = new();

    public NpzReader(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"npz dosyası bulunamadı: {path}", path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".npy", StringComparison.OrdinalIgnoreCase))
                continue;

            string name = entry.FullName[..^4];
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            _rawEntries[name] = ms.ToArray();
        }
    }

    /// <summary>npz içindeki dizi adları (uzantısız, "{name}.npy" → "{name}").</summary>
    public IReadOnlyCollection<string> Names => _rawEntries.Keys;

    public bool Contains(string name) => _rawEntries.ContainsKey(name);

    /// <summary>1 boyutlu float64 ('&lt;f8') dizi okur.</summary>
    public double[] ReadDoubleArray(string name)
    {
        var (descr, shape, data) = ParseNpy(name);
        if (descr != "<f8")
            throw new InvalidDataException($"'{name}' dtype '<f8' değil: '{descr}'.");
        if (shape.Length != 1)
            throw new InvalidDataException($"'{name}' 1 boyutlu değil (shape=({string.Join(",", shape)})).");

        var result = new double[shape[0]];
        for (int i = 0; i < result.Length; i++)
            result[i] = BitConverter.ToDouble(data, i * sizeof(double));
        return result;
    }

    /// <summary>2 boyutlu float64 ('&lt;f8', C-order) dizi okur.</summary>
    public double[,] ReadDouble2DArray(string name)
    {
        var (descr, shape, data) = ParseNpy(name);
        if (descr != "<f8")
            throw new InvalidDataException($"'{name}' dtype '<f8' değil: '{descr}'.");
        if (shape.Length != 2)
            throw new InvalidDataException($"'{name}' 2 boyutlu değil (shape=({string.Join(",", shape)})).");

        int rows = shape[0], cols = shape[1];
        var result = new double[rows, cols];
        int offset = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r, c] = BitConverter.ToDouble(data, offset);
                offset += sizeof(double);
            }
        }
        return result;
    }

    /// <summary>1 boyutlu int64 ('&lt;i8') dizi okur.</summary>
    public long[] ReadLongArray(string name)
    {
        var (descr, shape, data) = ParseNpy(name);
        if (descr != "<i8")
            throw new InvalidDataException($"'{name}' dtype '<i8' değil: '{descr}'.");
        if (shape.Length != 1)
            throw new InvalidDataException($"'{name}' 1 boyutlu değil (shape=({string.Join(",", shape)})).");

        var result = new long[shape[0]];
        for (int i = 0; i < result.Length; i++)
            result[i] = BitConverter.ToInt64(data, i * sizeof(long));
        return result;
    }

    /// <summary>1 boyutlu unicode string dizisi ('&lt;U{n}', UTF-32LE, null code point pad'li) okur.</summary>
    public string[] ReadStringArray(string name)
    {
        var (descr, shape, data) = ParseNpy(name);
        int maxCodepoints = ParseUnicodeWidth(descr, name);
        if (shape.Length != 1)
            throw new InvalidDataException($"'{name}' 1 boyutlu değil (shape=({string.Join(",", shape)})).");

        int strideBytes = maxCodepoints * 4;
        var result = new string[shape[0]];
        for (int i = 0; i < result.Length; i++)
            result[i] = Encoding.UTF32.GetString(data, i * strideBytes, strideBytes).TrimEnd('\0');
        return result;
    }

    /// <summary>0 boyutlu (scalar) unicode string okur — örn. meta_json.</summary>
    public string ReadScalarString(string name)
    {
        var (descr, shape, data) = ParseNpy(name);
        ParseUnicodeWidth(descr, name);
        if (shape.Length != 0)
            throw new InvalidDataException($"'{name}' scalar değil (shape=({string.Join(",", shape)})).");

        return Encoding.UTF32.GetString(data).TrimEnd('\0');
    }

    private static int ParseUnicodeWidth(string descr, string name)
    {
        var m = Regex.Match(descr, @"^<U(\d+)$");
        if (!m.Success)
            throw new InvalidDataException($"'{name}' beklenmeyen unicode dtype: '{descr}'.");
        return int.Parse(m.Groups[1].Value);
    }

    /// <summary>
    /// Ham "{name}.npy" byte'larını parse eder: magic + version + header (descr/shape) + veri.
    /// bkz. NpzWriter.BuildNpy (yazan taraf) — burası simetrik okuma tarafı.
    /// </summary>
    private (string Descr, int[] Shape, byte[] Data) ParseNpy(string name)
    {
        if (!_rawEntries.TryGetValue(name, out var bytes))
            throw new KeyNotFoundException($"npz içinde '{name}.npy' yok. Mevcut girdiler: {string.Join(", ", Names)}");

        if (bytes.Length < 10
            || bytes[0] != 0x93 || bytes[1] != (byte)'N' || bytes[2] != (byte)'U'
            || bytes[3] != (byte)'M' || bytes[4] != (byte)'P' || bytes[5] != (byte)'Y')
            throw new InvalidDataException($"'{name}.npy' geçerli bir .npy dosyası değil (magic eksik).");

        byte major = bytes[6];
        int headerLenFieldSize = major >= 2 ? 4 : 2;
        int headerLen = major >= 2
            ? BitConverter.ToInt32(bytes, 8)
            : BitConverter.ToUInt16(bytes, 8);

        int headerStart = 8 + headerLenFieldSize;
        string headerDict = Encoding.ASCII.GetString(bytes, headerStart, headerLen);

        var descrMatch = Regex.Match(headerDict, @"'descr':\s*'([^']+)'");
        var shapeMatch = Regex.Match(headerDict, @"'shape':\s*\(([^)]*)\)");
        if (!descrMatch.Success || !shapeMatch.Success)
            throw new InvalidDataException($"'{name}.npy' header parse edilemedi: {headerDict}");

        string descr = descrMatch.Groups[1].Value;
        string shapeInner = shapeMatch.Groups[1].Value.Trim();
        int[] shape = shapeInner.Length == 0
            ? Array.Empty<int>()
            : shapeInner.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s.Trim()))
                        .ToArray();

        int dataStart = headerStart + headerLen;
        int dataLen = bytes.Length - dataStart;
        var data = new byte[dataLen];
        Array.Copy(bytes, dataStart, data, 0, dataLen);

        return (descr, shape, data);
    }
}
