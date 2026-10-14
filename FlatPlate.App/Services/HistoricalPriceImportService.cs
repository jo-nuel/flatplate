using FlatPlate.Core.Data;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Services;

/// <summary>Inserts an explicitly confirmed historical batch; revalidates inside the transaction.</summary>
public sealed class HistoricalPriceImportService
{
    private readonly FlatPlateDbContext _context;

    public HistoricalPriceImportService(FlatPlateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public int Import(string evidencePath, IReadOnlyList<HistoricalPriceRecord> records,
        bool userConfirmed, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidencePath);
        ArgumentNullException.ThrowIfNull(records);
        if (!userConfirmed)
            throw new System.IO.InvalidDataException("Explicit confirmation of this historical batch is required. Nothing was saved.");

        return AppWriteTransaction.Execute(_context, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Read and hash the source again after the transaction starts. An earlier
            // validated preview cannot authorise changed evidence or database pairs.
            var evidence = HistoricalPriceEvidence.LoadInitial(evidencePath);
            var matches = new HistoricalPriceBatchValidator(_context, evidence).Validate(records);
            cancellationToken.ThrowIfCancellationRequested();

            var entries = matches.Select(match => new PriceEntry
            {
                IngredientId = match.IngredientId!.Value,
                StoreId = match.StoreId!.Value,
                Price = match.Record.PriceAUD,
                PackSize = match.Record.PackSizeBase,
                LastUpdated = match.Record.ToStoredLastUpdated(),
                Source = match.Record.Source,
            }).ToArray();
            _context.PriceEntries.AddRange(entries);
            cancellationToken.ThrowIfCancellationRequested();
            // The shared helper owns SaveChanges and commit, or rollback and tracker restoration.
            return entries.Length;
        });
    }
}
