using System.IO.Compression;
using System.Text;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>
/// numpy'nin np.load(path, allow_pickle=False) ile okuyabileceği minimal bir
/// .npz (.npy dizilerinin ZIP'i) yazıcısı. Harici bir numpy/Python bağımlılığı
/// olmadan, C# tarafında saf binary yazım ile üretir.
/// bkz. src/DearPyGuiDataPlotter/scripts/default.py (np.load ile okuyan taraf).
/// </summary>
public class NpzWriter
{
    private const int ArrayAlign = 64;

    private readonly List<(string Name, byte[] NpyBytes)> _entries = new();

    /// <summary>1 boyutlu float64 dizi ekler (dtype '&lt;f8').</summary>
    public void AddFloatArray(string name, IReadOnlyList<double> values)
    {
        var data = new byte[values.Count * sizeof(double)];
        for (int i = 0; i < values.Count; i++)
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(double));

        _entries.Add((name, BuildNpy("<f8", $"({values.Count},)", data)));
    }

    /// <summary>2 boyutlu float64 dizi ekler (dtype '&lt;f8', C-order/row-major).</summary>
    public void AddFloat2DArray(string name, double[,] values)
    {
        int rows = values.GetLength(0);
        int cols = values.GetLength(1);
        var data = new byte[rows * cols * sizeof(double)];
        int offset = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                BitConverter.GetBytes(values[r, c]).CopyTo(data, offset);
                offset += sizeof(double);
            }
        }

        _entries.Add((name, BuildNpy("<f8", $"({rows}, {cols})", data)));
    }

    /// <summary>1 boyutlu int64 dizi ekler (dtype '&lt;i8').</summary>
    public void AddIntArray(string name, IReadOnlyList<long> values)
    {
        var data = new byte[values.Count * sizeof(long)];
        for (int i = 0; i < values.Count; i++)
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(long));

        _entries.Add((name, BuildNpy("<i8", $"({values.Count},)", data)));
    }

    /// <summary>
    /// 1 boyutlu unicode string dizisi ekler (dtype '&lt;U{maxLen}', UTF-32LE,
    /// null code point ile sağa pad edilir - numpy'nin np.array(list_of_str)
    /// davranışıyla aynı, pickle GEREKTİRMEZ).
    /// </summary>
    public void AddStringArray(string name, IReadOnlyList<string> values)
    {
        var encoded = new byte[values.Count][];
        int maxCodepoints = 0;
        for (int i = 0; i < values.Count; i++)
        {
            encoded[i] = Encoding.UTF32.GetBytes(values[i] ?? "");
            maxCodepoints = Math.Max(maxCodepoints, encoded[i].Length / 4);
        }
        maxCodepoints = Math.Max(maxCodepoints, 1); // numpy '<U0' gecerli degil

        int strideBytes = maxCodepoints * 4;
        var data = new byte[values.Count * strideBytes];
        for (int i = 0; i < values.Count; i++)
            encoded[i].CopyTo(data, i * strideBytes);

        _entries.Add((name, BuildNpy($"<U{maxCodepoints}", $"({values.Count},)", data)));
    }

    /// <summary>0 boyutlu (scalar) unicode string ekler - örn. meta_json.</summary>
    public void AddScalarString(string name, string value)
    {
        value ??= "";
        var data = Encoding.UTF32.GetBytes(value);
        int codepoints = Math.Max(data.Length / 4, 1);
        if (data.Length == 0)
            data = new byte[4];

        _entries.Add((name, BuildNpy($"<U{codepoints}", "()", data)));
    }

    /// <summary>Toplanan tüm dizileri "{name}.npy" girdileri olarak .npz (zip) dosyasına yazar.</summary>
    public void Save(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string tempPath = path + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, npyBytes) in _entries)
            {
                var entry = archive.CreateEntry(name + ".npy", CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(npyBytes, 0, npyBytes.Length);
            }
        }

        if (File.Exists(path))
            File.Delete(path);
        File.Move(tempPath, path);
    }

    /// <summary>
    /// Tek bir .npy dosyasının tam byte içeriğini üretir: magic + version +
    /// header (descr/fortran_order/shape) + ham veri. bkz. numpy .npy v1.0 format.
    /// </summary>
    private static byte[] BuildNpy(string descr, string shapeTuple, byte[] data)
    {
        string headerDict = $"{{'descr': '{descr}', 'fortran_order': False, 'shape': {shapeTuple}, }}";

        const int preambleLen = 6 + 2 + 2; // magic + version + header_len (v1.0, 2 byte)
        int unpaddedTotal = preambleLen + headerDict.Length + 1; // +1 = trailing '\n'
        int padding = (ArrayAlign - (unpaddedTotal % ArrayAlign)) % ArrayAlign;
        string header = headerDict + new string(' ', padding) + "\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        var result = new byte[preambleLen + headerBytes.Length + data.Length];

        int pos = 0;
        result[pos++] = 0x93;
        result[pos++] = (byte)'N';
        result[pos++] = (byte)'U';
        result[pos++] = (byte)'M';
        result[pos++] = (byte)'P';
        result[pos++] = (byte)'Y';
        result[pos++] = 1; // major version
        result[pos++] = 0; // minor version

        byte[] headerLenBytes = BitConverter.GetBytes((ushort)headerBytes.Length);
        headerLenBytes.CopyTo(result, pos);
        pos += 2;

        headerBytes.CopyTo(result, pos);
        pos += headerBytes.Length;

        data.CopyTo(result, pos);

        return result;
    }
}
