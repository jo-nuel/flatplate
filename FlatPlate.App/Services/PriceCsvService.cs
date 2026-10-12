using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FlatPlate.Core.Enums;

namespace FlatPlate.App.Services;

/// <summary>Parses, validates and exports price CSV V1 using explicitly supplied known names.</summary>
public sealed class PriceCsvService
{
    public const string Header = "IngredientName,StoreName,PriceAUD,PackSizeBase,BaseUnit,LastUpdated,Source";

    private static readonly string[] Columns = Header.Split(',');
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal) { "g", "mL", "pieces" };
    private static readonly HashSet<string> Sources = new(Enum.GetNames<PriceSource>(), StringComparer.Ordinal);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex DecimalPattern = new(@"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex TimestampPattern = new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})?$", RegexOptions.CultureInvariant);
    private static readonly string[] WallTimeFormats = { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" };
    private static readonly string[] OffsetFormats = { "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz" };

    private readonly HashSet<string> _ingredientNames;
    private readonly HashSet<string> _storeNames;

    /// <summary>Uses exact known-name matches; no aliases, IDs or database queries are inferred.</summary>
    public PriceCsvService(IEnumerable<string> ingredientNames, IEnumerable<string> storeNames)
    {
        _ingredientNames = KnownNames(ingredientNames, nameof(ingredientNames));
        _storeNames = KnownNames(storeNames, nameof(storeNames));
    }

    /// <summary>Exports only the supplied records. The caller chooses visible or selected rows.</summary>
    public string Export(IEnumerable<PriceCsvRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var output = new StringBuilder(Header).Append("\r\n");
        var recordNumber = 0;

        foreach (var record in records)
        {
            recordNumber++;
            if (record is null)
            {
                throw new ArgumentException($"Record {recordNumber} is null.", nameof(records));
            }

            var fields = new[]
            {
                record.IngredientName, record.StoreName,
                record.PriceAUD.ToString(CultureInfo.InvariantCulture),
                record.PackSizeBase.ToString(CultureInfo.InvariantCulture),
                record.BaseUnit, record.LastUpdated, record.Source.ToString(),
            };
            var errors = new List<PriceCsvRowError>();
            _ = Validate(fields, recordNumber, errors);
            if (errors.Count > 0)
            {
                throw new ArgumentException($"Record {recordNumber}: {string.Join(" ", errors.Select(error => error.Message))}", nameof(records));
            }

            output.AppendJoin(',', fields.Select(Escape)).Append("\r\n");
        }

        return output.ToString();
    }

    /// <summary>Returns valid rows and physical source-line errors without importing any data.</summary>
    public PriceCsvParseResult Parse(string csvText)
    {
        ArgumentNullException.ThrowIfNull(csvText);
        if (csvText.StartsWith('\uFEFF')) csvText = csvText[1..];
        var rows = ReadRows(csvText);
        var records = new List<PriceCsvRecord>();
        var errors = new List<PriceCsvRowError>();

        if (rows.Count == 0)
        {
            errors.Add(new PriceCsvRowError(1, "Header", "CSV input is empty; the V1 header is required."));
            return new PriceCsvParseResult(records, errors);
        }

        var header = rows[0];
        if (header.SyntaxError is not null || !header.Fields.SequenceEqual(Columns, StringComparer.Ordinal))
        {
            errors.Add(new PriceCsvRowError(header.StartLine, "Header", $"Expected the exact V1 header order: {Header}."));
            return new PriceCsvParseResult(records, errors);
        }

        foreach (var row in rows.Skip(1))
        {
            if (row.IsBlank) continue;
            if (row.SyntaxError is not null)
            {
                errors.Add(new PriceCsvRowError(row.StartLine, "CSV", row.SyntaxError));
                continue;
            }
            if (row.Fields.Count != Columns.Length)
            {
                errors.Add(new PriceCsvRowError(row.StartLine, "CSV", $"Expected 7 fields, found {row.Fields.Count}."));
                continue;
            }

            var record = Validate(row.Fields, row.StartLine, errors);
            if (record is not null) records.Add(record);
        }

        return new PriceCsvParseResult(records, errors);
    }

    private PriceCsvRecord? Validate(IReadOnlyList<string> fields, int line, List<PriceCsvRowError> errors)
    {
        var initialErrors = errors.Count;
        ValidateName(fields[0], _ingredientNames, "IngredientName");
        ValidateName(fields[1], _storeNames, "StoreName");
        var price = PositiveDecimal(fields[2], "PriceAUD");
        var packSize = PositiveDecimal(fields[3], "PackSizeBase");
        if (!Units.Contains(fields[4])) Error("BaseUnit", "BaseUnit must be exactly g, mL or pieces.");
        if (!ValidTimestamp(fields[5])) Error("LastUpdated", "LastUpdated must be a valid ISO timestamp, with no zone, Z or an explicit offset.");
        var source = default(PriceSource);
        if (!Sources.Contains(fields[6]) || !Enum.TryParse(fields[6], out source))
        {
            Error("Source", "Source must be an existing named PriceSource value; numbers and unknown values are not accepted.");
        }

        return errors.Count == initialErrors
            ? new PriceCsvRecord(fields[0], fields[1], price, packSize, fields[4], fields[5], source)
            : null;

        void Error(string column, string message) => errors.Add(new PriceCsvRowError(line, column, message));

        void ValidateName(string name, HashSet<string> known, string column)
        {
            if (string.IsNullOrWhiteSpace(name)) Error(column, $"{column} is required.");
            else if (!known.Contains(name)) Error(column, $"Unknown {column}; exact known names are required without aliases or trimming.");
        }

        decimal PositiveDecimal(string text, string column)
        {
            if (string.IsNullOrEmpty(text) || !DecimalPattern.IsMatch(text)
                || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value))
            {
                Error(column, $"{column} must be an invariant decimal without separators or exponent notation.");
                return 0m;
            }
            if (NormaliseDecimal(text) != NormaliseDecimal(value.ToString(CultureInfo.InvariantCulture)))
            {
                Error(column, $"{column} exceeds decimal precision; rounding is not accepted.");
            }
            if (value <= 0m) Error(column, $"{column} must be greater than zero.");
            return value;
        }
    }

    private static HashSet<string> KnownNames(IEnumerable<string> names, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(names, parameterName);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Known names must not be empty.", parameterName);
            try { _ = StrictUtf8.GetByteCount(name); }
            catch (EncoderFallbackException exception) { throw new ArgumentException("Known names must contain valid Unicode.", parameterName, exception); }
            result.Add(name);
        }
        return result;
    }

    private static bool ValidTimestamp(string text)
    {
        if (string.IsNullOrEmpty(text) || !TimestampPattern.IsMatch(text)) return false;
        if (text.EndsWith('Z'))
        {
            return DateTime.TryParseExact(text[..^1], WallTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }
        if (text[^6] is '+' or '-')
        {
            return DateTimeOffset.TryParseExact(text, OffsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }
        return DateTime.TryParseExact(text, WallTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static string NormaliseDecimal(string text)
    {
        var negative = text.StartsWith('-');
        if (text.StartsWith('+') || negative) text = text[1..];
        var parts = text.Split('.');
        var integer = parts[0].TrimStart('0');
        if (integer.Length == 0) integer = "0";
        var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : string.Empty;
        var number = fraction.Length == 0 ? integer : integer + "." + fraction;
        return negative && number != "0" ? "-" + number : number;
    }

    private static string Escape(string field)
    {
        return field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
    }

    private enum FieldState { Start, Unquoted, Quoted, AfterQuote }

    private sealed record CsvRow(List<string> Fields, int StartLine, bool IsBlank, string? SyntaxError);

    private static List<CsvRow> ReadRows(string text)
    {
        var rows = new List<CsvRow>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var state = FieldState.Start;
        var line = 1;
        var startLine = 1;
        var hasCharacters = false;
        string? error = null;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (state == FieldState.Quoted)
            {
                if (character == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else state = FieldState.AfterQuote;
                }
                else
                {
                    field.Append(character);
                    if (character == '\r')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\n') { field.Append('\n'); i++; }
                        line++;
                    }
                    else if (character == '\n') line++;
                }
                continue;
            }

            if (character is '\r' or '\n')
            {
                AddField();
                rows.Add(new CsvRow(fields, startLine, !hasCharacters, error));
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                line++;
                startLine = line;
                fields = new List<string>();
                hasCharacters = false;
                error = null;
                continue;
            }

            hasCharacters = true;
            if (character == ',') { AddField(); continue; }
            if (character == '"' && state == FieldState.Start) { state = FieldState.Quoted; continue; }
            if (character == '"') error ??= "Unexpected quote in an unquoted field.";
            if (state == FieldState.AfterQuote) error ??= "Unexpected character after a closing quote.";
            field.Append(character);
            state = FieldState.Unquoted;
        }

        if (state == FieldState.Quoted) error ??= "Unterminated quoted field; following physical lines cannot be recovered safely.";
        if (hasCharacters || fields.Count > 0 || field.Length > 0 || state != FieldState.Start)
        {
            AddField();
            rows.Add(new CsvRow(fields, startLine, false, error));
        }

        return rows;

        void AddField()
        {
            fields.Add(field.ToString());
            field.Clear();
            state = FieldState.Start;
        }
    }
}
