using FlatPlate.Core.Data;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Services;

/// <summary>Inserts a reviewed historical batch using the host context; never trusts preview approval.</summary>
public sealed class HistoricalPriceImportService
{
    private readonly FlatPlateDbContext _context;
    private readonly TimeProvider _clock;

    public HistoricalPriceImportService(FlatPlateDbContext context, TimeProvider? clock = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _clock = clock ?? TimeProvider.System;
    }

    public int Import(string evidencePath, IReadOnlyList<HistoricalPriceRecord> records,
        HistoricalPriceApprovalReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidencePath);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(receipt);

        return AppWriteTransaction.Execute(_context, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Read and hash the source again after the transaction starts. An earlier
            // approved preview cannot authorise changed files, receipts or database pairs.
            var evidence = HistoricalPriceEvidence.LoadInitial(evidencePath);
            var matches = new HistoricalPriceApprovalService(_context, evidence, _clock)
                .Validate(records, receipt);
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
