using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace FlatPlate.App.Services;

/// <summary>Immutable initial evidence; later human sign-off must be supplied separately.</summary>
public sealed class HistoricalPriceEvidence
{
    public const string InitialEvidenceSha256 = "aaa279633e551e3b45e1f61d88702d6298504c8da3d4f056c1d0a7fad9bb2995";
    private const string CanonicalEvidenceSha256 = "f4b848c8c73b6df8013b05c424c5e2797c52e916c0006556ac158e379f356050";
    public const string DatasetRevision = "4f802169940df31e09f2612f6a6288ffd72dc4fd";
    private HistoricalPriceEvidence(IReadOnlyList<HistoricalPriceEvidenceRow> rows) => Rows = rows;
    public IReadOnlyList<HistoricalPriceEvidenceRow> Rows { get; }

    public static HistoricalPriceEvidence LoadInitial(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Historical evidence must be valid UTF-8.", error); }
        // Accept BOM/line-ending changes from Git, without normalising field values.
        var canonical = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant() != CanonicalEvidenceSha256)
            throw new InvalidDataException("Historical evidence changed. Review the version instead of accepting changed prices or approvals.");
        using var input = new StringReader(text);
        using var parser = new TextFieldParser(input) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        try
        {
            var header = parser.ReadFields() ?? throw new InvalidDataException("Historical evidence is empty.");
            if (header.Length != 70 || header.Distinct(StringComparer.Ordinal).Count() != 70)
                throw new InvalidDataException("Historical evidence must retain its original seventy fields.");
            var columns = header.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);
            var fields = new List<string[]>();
            while (!parser.EndOfData)
            {
                var row = parser.ReadFields()!;
                if (row.Length != header.Length) throw new InvalidDataException("Historical evidence has an incorrect field count.");
                fields.Add(row);
            }
            if (fields.Count != 40) throw new InvalidDataException("The initial evidence must contain forty candidate pairs.");
            string Value(string[] row, string name) => row[columns[name]];
            foreach (var row in fields)
                if (Value(row, "DatasetRevision") != DatasetRevision || Value(row, "ObservationDate") != "2026-09-17"
                    || Value(row, "UserConfirmed") != "false" || Value(row, "ApprovedForImport") != "false"
                    || Value(row, "FinalHumanApproval") != "Pending")
                    throw new InvalidDataException("Initial evidence must retain the pinned snapshot and pending human approvals.");

            // Reuse the reviewed historical reader, which in turn reuses frozen V1 validation.
            var preparation = HistoricalPriceCsvReader.Header + "\r\n" + string.Join("\r\n", fields.Select(row =>
                string.Join(',', new[] { "IngredientName", "StoreName", "PriceAUD", "PackSizeBase", "BaseUnit",
                    "ObservationDate", "ProductId", "InStockAtSnapshot" }.Select(name => Quote(Value(row, name)))))) + "\r\n";
            var records = new HistoricalPriceCsvReader().Parse(preparation,
                fields.Select(row => Value(row, "IngredientName")), fields.Select(row => Value(row, "StoreName")));
            var evidence = records.Select((record, index) => new HistoricalPriceEvidenceRow(record,
                Value(fields[index], "IngredientType"), false, false, Value(fields[index], "FinalHumanApproval"))).ToArray();
            return new HistoricalPriceEvidence(Array.AsReadOnly(evidence));
        }
        catch (MalformedLineException error)
        {
            throw new InvalidDataException($"Malformed historical evidence at line {error.LineNumber}.", error);
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}

public sealed record HistoricalPriceEvidenceRow(HistoricalPriceRecord Record, string IngredientType,
    bool UserConfirmed, bool ApprovedForImport, string FinalHumanApproval);
