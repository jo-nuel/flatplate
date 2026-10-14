using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatPlate.App.Services;

public sealed record HistoricalPriceFileSelection(string CsvPath, string EvidencePath, string ReceiptPath);
public sealed record HistoricalPriceImportPackage(HistoricalPriceFileSelection Files,
    IReadOnlyList<HistoricalPriceRecord> Records, HistoricalPriceEvidence Evidence,
    HistoricalPriceApprovalReceipt Receipt, string CsvHash, string EvidenceHash, string ReceiptHash);

/// <summary>Reads selected files without granting approval or saving prices.</summary>
public sealed class HistoricalPriceImportFiles
{
    private readonly PriceCsvFileService _files = new();

    public HistoricalPriceImportPackage Read(HistoricalPriceFileSelection files,
        IEnumerable<string> ingredientNames, IEnumerable<string> storeNames)
    {
        var csvHash = Hash(files.CsvPath); var evidenceHash = Hash(files.EvidencePath); var receiptHash = Hash(files.ReceiptPath);
        var names = ingredientNames.ToArray(); var stores = storeNames.ToArray();
        var records = new HistoricalPriceCsvReader().Parse(_files.Read(files.CsvPath), names, stores);
        var evidence = HistoricalPriceEvidence.LoadInitial(files.EvidencePath);
        HistoricalPriceApprovalReceipt receipt;
        try
        {
            using var json = JsonDocument.Parse(_files.Read(files.ReceiptPath));
            RejectDuplicateProperties(json.RootElement);
            receipt = json.Deserialize<HistoricalPriceApprovalReceipt>(new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            }) ?? throw new InvalidDataException("The approval receipt is empty.");
            // Reuse exact-decimal CSV validation on the original JSON numeric text;
            // decimal deserialization alone may round excessive precision.
            var items = receipt.Items ?? throw new InvalidDataException("The receipt needs explicit review items.");
            if (items.Any(i => i is null || i.Record is null)) throw new InvalidDataException("A receipt item is incomplete.");
            var raw = json.RootElement.GetProperty("Items").EnumerateArray().ToArray();
            var projection = HistoricalPriceCsvReader.Header + "\r\n" + string.Join("\r\n", items.Select((item, index) =>
                string.Join(',', new[] { item.Record.IngredientName, item.Record.StoreName,
                    raw[index].GetProperty("Record").GetProperty("PriceAUD").GetRawText(),
                    raw[index].GetProperty("Record").GetProperty("PackSizeBase").GetRawText(),
                    item.Record.BaseUnit, item.Record.ObservedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), item.Record.ProductId,
                    item.Record.InStockAtObservation ? "true" : "false" }.Select(Quote)))) + "\r\n";
            _ = new HistoricalPriceCsvReader().Parse(projection, names, stores);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new InvalidDataException("Invalid approval receipt JSON: " + error.Message, error); }
        var result = new HistoricalPriceImportPackage(files, records, evidence, receipt, csvHash, evidenceHash, receiptHash);
        RequireUnchanged(result);
        return result;
    }

    public void RequireUnchanged(HistoricalPriceImportPackage package)
    {
        if (Hash(package.Files.CsvPath) != package.CsvHash || Hash(package.Files.EvidencePath) != package.EvidenceHash
            || Hash(package.Files.ReceiptPath) != package.ReceiptHash)
            throw new InvalidDataException("A selected file changed after preview. Cancel and select all three files again.");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Quote(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate receipt field: " + property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
}
