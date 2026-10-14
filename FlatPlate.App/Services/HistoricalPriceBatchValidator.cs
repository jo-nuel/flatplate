using System.IO;
using FlatPlate.Core.Data;

namespace FlatPlate.App.Services;

/// <summary>Checks the complete pinned batch against its evidence and current database; never writes.</summary>
public sealed class HistoricalPriceBatchValidator
{
    private readonly HistoricalPriceEvidence _evidence;
    private readonly HistoricalPriceMatchService _matcher;

    public HistoricalPriceBatchValidator(FlatPlateDbContext context, HistoricalPriceEvidence evidence)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _matcher = new HistoricalPriceMatchService(context, evidence);
    }

    public IReadOnlyList<HistoricalPriceMatch> Validate(IReadOnlyList<HistoricalPriceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count != _evidence.Rows.Count || records.Any(r => r is null)
            || records.GroupBy(r => (r.IngredientName, r.StoreName)).Any(g => g.Count() != 1))
            throw new InvalidDataException("Select the complete forty-row historical batch with unique ingredient/store pairs.");

        // Compare every value (including SKU, date and stock) with hash-checked
        // evidence, then recheck database identities, ingredient units and existing prices.
        var matches = _matcher.Review(records);
        var blocked = matches.Where(m => !m.TechnicallyMatched).ToArray();
        if (blocked.Length != 0)
            throw new InvalidDataException(string.Join(" ", blocked.Select(m =>
                $"{m.Record.IngredientName}/{m.Record.StoreName}: {string.Join(" ", m.TechnicalIssues)}")));
        return matches;
    }
}
