namespace FlatPlate.App.Services;

/// <summary>
/// Separates valid records from errors; neither collection writes to a database.
/// </summary>
public sealed class PriceCsvParseResult
{
    internal PriceCsvParseResult(List<PriceCsvRecord> records, List<PriceCsvRowError> errors)
    {
        Records = records.AsReadOnly();
        Errors = errors.AsReadOnly();
    }

    public IReadOnlyList<PriceCsvRecord> Records { get; }

    public IReadOnlyList<PriceCsvRowError> Errors { get; }

    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Identifies the one-based physical start line of an invalid CSV record.
/// </summary>
public sealed record PriceCsvRowError(int LineNumber, string ColumnName, string Message);
