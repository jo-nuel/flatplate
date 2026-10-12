using System.IO;
using System.Text;

namespace FlatPlate.App.Services;

/// <summary>Ordinary UTF-8 file operations. No catalogue, context, import or save of prices.</summary>
public sealed class PriceCsvFileService
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public string Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var start = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        try { return Utf8.GetString(bytes, start, bytes.Length - start); }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The CSV file must contain valid UTF-8 text.", exception);
        }
    }

    public void Write(string path, string csvText) => File.WriteAllText(path, csvText, Utf8);
}
