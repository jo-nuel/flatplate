using System.IO;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Read-only catalogue, pair and evidence matching; results never authorise a save.</summary>
public sealed class HistoricalPriceMatchService
{
    private readonly FlatPlateDbContext _context;
    private readonly HistoricalPriceEvidence _evidence;
    public HistoricalPriceMatchService(FlatPlateDbContext context, HistoricalPriceEvidence evidence)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    public IReadOnlyList<HistoricalPriceMatch> Review(IReadOnlyList<HistoricalPriceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0 || records.Any(r => r is null))
            throw new InvalidDataException("Review requires nonempty historical records.");
        var ingredients = _context.Ingredients.AsNoTracking().ToList();
        var stores = _context.Stores.AsNoTracking().ToList();
        var ingredientNames = ingredients.ToLookup(i => i.Name, StringComparer.Ordinal);
        var storeNames = stores.ToLookup(s => s.Name, StringComparer.Ordinal);
        var ingredientCases = ingredients.ToLookup(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var storeCases = stores.ToLookup(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var pairs = _context.PriceEntries.AsNoTracking().Select(p => new { p.IngredientId, p.StoreId })
            .ToList().Select(p => (p.IngredientId, p.StoreId)).ToHashSet();
        var evidence = _evidence.Rows.ToDictionary(e => (e.Record.IngredientName, e.Record.StoreName));
        var duplicateKeys = records.GroupBy(r => (r.IngredientName, r.StoreName))
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var results = new List<HistoricalPriceMatch>();
        foreach (var record in records)
        {
            var issues = new List<string>();
            try { record.ValidateForReview(); }
            catch (InvalidDataException error) { issues.Add(error.Message); }
            var key = (record.IngredientName, record.StoreName);
            evidence.TryGetValue(key, out var reference);
            if (reference is null) issues.Add("No selected snapshot evidence for this ingredient/store.");
            else if (record != reference.Record) issues.Add("Snapshot SKU, price, pack, unit, date or stock evidence differs.");
            if (duplicateKeys.Contains(key)) issues.Add("Duplicate ingredient/store in the review batch.");

            var ingredientMatches = ingredientNames[record.IngredientName].ToArray();
            var storeMatches = storeNames[record.StoreName].ToArray();
            var ingredient = ingredientMatches.Length == 1 && ingredientCases[record.IngredientName].Count() == 1 ? ingredientMatches[0] : null;
            var store = storeMatches.Length == 1 && storeCases[record.StoreName].Count() == 1 ? storeMatches[0] : null;
            if (ingredient is null) issues.Add("Missing or ambiguous exact ingredient name. Import the reviewed recipes first; no alias is inferred.");
            if (store is null) issues.Add("Missing or ambiguous exact store name.");
            if (ingredient is not null && (PriceBookRowViewModel.GetBaseUnit(ingredient) != record.BaseUnit
                || reference is not null && ingredient.GetType().Name != reference.IngredientType))
                issues.Add("Ingredient subtype/base unit differs from the historical evidence.");
            if (ingredient is not null && store is not null && pairs.Contains((ingredient.Id, store.Id)))
                issues.Add("A current price already exists for this ingredient/store; replacement is prohibited.");

            // User selected this exact historical oil reference in B017C/D.
            // It remains stock=false and is not personal verification or import authority.
            var oilPolicy = reference is not null && record == reference.Record
                && record.IngredientName == "Vegetable oil" && record.StoreName == "Coles"
                && record.ProductId == "126745" && record.PriceAUD == 4.80m && record.PackSizeBase == 750m
                && record.BaseUnit == "mL" && record.ObservedDate == new DateOnly(2026, 9, 17) && !record.InStockAtObservation;
            results.Add(new HistoricalPriceMatch(record, ingredient?.Id, store?.Id, issues.AsReadOnly(),
                reference?.UserConfirmed ?? false, reference?.ApprovedForImport ?? false,
                reference?.FinalHumanApproval ?? "Pending", oilPolicy));
        }
        return results.AsReadOnly();
    }
}

public sealed record HistoricalPriceMatch(HistoricalPriceRecord Record, int? IngredientId, int? StoreId,
    IReadOnlyList<string> TechnicalIssues, bool UserConfirmed, bool ApprovedForImport,
    string FinalHumanApproval, bool OilHistoricalReferencePolicyAccepted)
{
    public bool TechnicallyMatched => IngredientId.HasValue && StoreId.HasValue && TechnicalIssues.Count == 0;
    // Original flags describe archived research, not the current import confirmation.
}
