using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Inserts a fully validated CSV batch; never replaces a current price or owns the context.</summary>
public sealed class PriceCsvImportService
{
    private readonly FlatPlateDbContext _context;
    public PriceCsvImportService(FlatPlateDbContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public int Import(PriceCsvParseResult parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        return AppWriteTransaction.Execute(_context, () =>
        {
            var ingredients = _context.Ingredients.AsNoTracking().ToList();
            var stores = _context.Stores.AsNoTracking().ToList();
            var keys = _context.PriceEntries.AsNoTracking().Select(p => new { p.IngredientId, p.StoreId }).ToList();
            var review = new PriceCsvImportPreviewViewModel(parsed, ingredients, stores,
                keys.Select(p => (p.IngredientId, p.StoreId)), forImport: true);
            if (!review.IsBatchEligible) throw new PriceCsvImportValidationException(review.Issues);

            // Reuse the frozen parser/export validator; never accept a partial parse result.
            _ = new PriceCsvService(ingredients.Select(i => i.Name), stores.Select(s => s.Name)).Export(parsed.Records);
            var conflicts = review.Rows.Where(row => _context.PriceEntries.AsNoTracking().Any(p =>
                p.IngredientId == row.IngredientId!.Value && p.StoreId == row.StoreId!.Value))
                .Select(row => new PriceCsvPreviewIssue($"Parsed row {row.ParsedRow}", "Duplicate", "This price pair now exists. Close and select the CSV again; nothing was saved.")).ToArray();
            if (conflicts.Length > 0) throw new PriceCsvImportValidationException(conflicts);

            var entries = review.Rows.Select(row => new PriceEntry
            {
                IngredientId = row.IngredientId!.Value,
                StoreId = row.StoreId!.Value,
                Price = row.Record.PriceAUD,
                PackSize = row.Record.PackSizeBase,
                LastUpdated = row.ObservedUtc!.Value,
                Source = row.Record.Source,
            }).ToArray();
            _context.PriceEntries.AddRange(entries);
            return entries.Length;
        });
    }
}

/// <summary>Fresh database checks can invalidate an otherwise eligible preview.</summary>
public sealed class PriceCsvImportValidationException : InvalidOperationException
{
    public PriceCsvImportValidationException(IEnumerable<PriceCsvPreviewIssue> issues)
        : base("The entire import is blocked. Review the reported problems; no records were saved.")
        => Issues = issues.ToArray();
    public IReadOnlyList<PriceCsvPreviewIssue> Issues { get; }
}
