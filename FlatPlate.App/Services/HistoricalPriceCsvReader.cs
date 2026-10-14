using System.Globalization;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace FlatPlate.App.Services;

/// <summary>Reads historical preparation rows without saving or granting human approval.</summary>
public sealed class HistoricalPriceCsvReader
{
    public const string Header = "IngredientName,StoreName,PriceAUD,PackSizeBase,BaseUnit,ObservedDate,ProductId,InStockAtObservation";

    public IReadOnlyList<HistoricalPriceRecord> Parse(string csvText,
        IEnumerable<string> ingredientNames, IEnumerable<string> storeNames)
    {
        ArgumentNullException.ThrowIfNull(csvText);
        var validator = new PriceCsvService(ingredientNames, storeNames);
        using var input = new StringReader(csvText.TrimStart('\uFEFF'));
        using var parser = new TextFieldParser(input) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        try
        {
            if (parser.EndOfData || !parser.ReadFields()!.SequenceEqual(Header.Split(','), StringComparer.Ordinal))
                throw new InvalidDataException($"Expected the exact historical preparation header: {Header}.");
            var records = new List<HistoricalPriceRecord>();
            while (!parser.EndOfData)
            {
                var line = parser.LineNumber;
                var fields = parser.ReadFields()!;
                if (fields.Length != 8) throw new InvalidDataException($"Historical CSV line {line} must have eight fields.");
                var date = HistoricalPriceRecord.ParseObservedDate(fields[5]);
                if (fields[7] is not ("true" or "false"))
                    throw new InvalidDataException($"Historical CSV line {line} stock evidence must be true or false.");

                // Reuse frozen V1 numeric/name/unit validation. This internal projection
                // encodes an unknown-time date; it is never sent to the V1 importer.
                var encodedDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)
                    .ToString("O", CultureInfo.InvariantCulture);
                var projected = PriceCsvService.Header + "\r\n" + string.Join(',', new[]
                    { fields[0], fields[1], fields[2], fields[3], fields[4], encodedDate, "Seeded" }.Select(Quote)) + "\r\n";
                var parsed = validator.Parse(projected);
                if (parsed.Errors.Count != 0)
                    throw new InvalidDataException($"Historical CSV line {line}: {string.Join(" ", parsed.Errors.Select(e => e.Message))}");
                var value = parsed.Records.Single();
                var record = new HistoricalPriceRecord(value.IngredientName, value.StoreName,
                    value.PriceAUD, value.PackSizeBase, value.BaseUnit, date, fields[6], fields[7] == "true");
                record.ValidateForReview();
                records.Add(record);
            }
            if (records.Count == 0 || records.GroupBy(r => (r.IngredientName, r.StoreName)).Any(g => g.Count() != 1))
                throw new InvalidDataException("A historical batch must contain rows with unique ingredient/store pairs.");
            return records.AsReadOnly();
        }
        catch (MalformedLineException error)
        {
            throw new InvalidDataException($"Malformed historical CSV at line {error.LineNumber}.", error);
        }
    }

    private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
}
