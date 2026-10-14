using System.IO;
using System.Security.Cryptography;

namespace FlatPlate.App.Services;

public sealed record HistoricalPriceFileSelection(string CsvPath, string EvidencePath);
public sealed record HistoricalPriceImportPackage(HistoricalPriceFileSelection Files,
    IReadOnlyList<HistoricalPriceRecord> Records, HistoricalPriceEvidence Evidence,
    string CsvHash, string EvidenceHash);

/// <summary>Reads the price CSV and pinned evidence without saving prices.</summary>
public sealed class HistoricalPriceImportFiles
{
    private readonly PriceCsvFileService _files = new();

    public HistoricalPriceImportPackage Read(HistoricalPriceFileSelection files,
        IEnumerable<string> ingredientNames, IEnumerable<string> storeNames)
    {
        var csvHash = Hash(files.CsvPath);
        var evidenceHash = Hash(files.EvidencePath);
        var records = new HistoricalPriceCsvReader().Parse(_files.Read(files.CsvPath), ingredientNames, storeNames);
        var evidence = HistoricalPriceEvidence.LoadInitial(files.EvidencePath);
        var result = new HistoricalPriceImportPackage(files, records, evidence, csvHash, evidenceHash);
        RequireUnchanged(result);
        return result;
    }

    public void RequireUnchanged(HistoricalPriceImportPackage package)
    {
        if (Hash(package.Files.CsvPath) != package.CsvHash || Hash(package.Files.EvidencePath) != package.EvidenceHash)
            throw new InvalidDataException("A selected file changed after preview. Cancel and select both files again.");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
