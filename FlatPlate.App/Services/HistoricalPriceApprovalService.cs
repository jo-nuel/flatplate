using System.IO;
using FlatPlate.Core.Data;

namespace FlatPlate.App.Services;

/// <summary>A separate reviewer attestation binds each decision to the unchanged historical evidence.</summary>
public sealed record HistoricalPriceApprovalItem(HistoricalPriceRecord Record,
    bool PersonallyReviewed, bool ApprovedForImport);

public sealed record HistoricalPriceApprovalReceipt(string ReviewerName, DateOnly ReviewedOn,
    string EvidenceSha256, string DatasetRevision, IReadOnlyList<HistoricalPriceApprovalItem> Items);

/// <summary>Validates a whole reviewed batch and its current database match; never writes or creates approval.</summary>
public sealed class HistoricalPriceApprovalService
{
    private readonly HistoricalPriceEvidence _evidence;
    private readonly HistoricalPriceMatchService _matcher;
    private readonly TimeProvider _clock;

    public HistoricalPriceApprovalService(FlatPlateDbContext context, HistoricalPriceEvidence evidence,
        TimeProvider? clock = null)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _matcher = new HistoricalPriceMatchService(context, evidence);
        _clock = clock ?? TimeProvider.System;
    }

    public IReadOnlyList<HistoricalPriceMatch> Validate(IReadOnlyList<HistoricalPriceRecord> records,
        HistoricalPriceApprovalReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(receipt);
        if (string.IsNullOrWhiteSpace(receipt.ReviewerName) || receipt.ReviewerName != receipt.ReviewerName.Trim()
            || receipt.ReviewedOn == default)
            throw new InvalidDataException("A real reviewer name and review date must be supplied; no review is inferred.");
        var actualReviewDate = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        if (receipt.ReviewedOn > actualReviewDate)
            throw new InvalidDataException("Review date cannot be later than the actual local validation date.");
        if (receipt.EvidenceSha256 != HistoricalPriceEvidence.InitialEvidenceSha256
            || receipt.DatasetRevision != HistoricalPriceEvidence.DatasetRevision)
            throw new InvalidDataException("Approval refers to a different historical evidence version.");
        var candidates = records.ToArray();
        var approvals = receipt.Items?.ToArray();
        if (candidates.Length != _evidence.Rows.Count || candidates.Any(r => r is null)
            || approvals is null || approvals.Length != candidates.Length || approvals.Any(a => a is null || a.Record is null))
            throw new InvalidDataException("The selected forty-row batch needs one explicit approval for every record.");
        if (candidates.GroupBy(r => (r.IngredientName, r.StoreName)).Any(g => g.Count() != 1)
            || approvals.GroupBy(a => (a.Record.IngredientName, a.Record.StoreName)).Any(g => g.Count() != 1))
            throw new InvalidDataException("Candidate and approval ingredient/store pairs must be unique.");
        var originals = _evidence.Rows.ToDictionary(e => (e.Record.IngredientName, e.Record.StoreName));
        var reviewed = approvals.ToDictionary(a => (a.Record.IngredientName, a.Record.StoreName));
        foreach (var record in candidates)
        {
            var key = (record.IngredientName, record.StoreName);
            if (!originals.TryGetValue(key, out var original) || record != original.Record
                || !reviewed.TryGetValue(key, out var approval) || approval.Record != original.Record)
                throw new InvalidDataException("Approval must bind the original ingredient/store, SKU, price, pack, unit, date and stock evidence.");
            if (!approval.PersonallyReviewed || !approval.ApprovedForImport)
                throw new InvalidDataException($"Personal review/import approval is pending for {record.IngredientName} at {record.StoreName}. Oil inclusion policy is not personal approval.");
            if (receipt.ReviewedOn < record.ObservedDate)
                throw new InvalidDataException("Review date cannot precede the historical observation date.");
        }
        var matches = _matcher.Review(candidates);
        var blocked = matches.Where(m => !m.TechnicallyMatched).ToArray();
        if (blocked.Length != 0)
            throw new InvalidDataException(string.Join(" ", blocked.Select(m =>
                $"{m.Record.IngredientName}/{m.Record.StoreName}: {string.Join(" ", m.TechnicalIssues)}")));
        // Initial evidence flags remain false/Pending. Only the separately supplied
        // attestation passed this check; a future writer must repeat it inside its transaction.
        // Copy the matches so a validated preview displays the receipt decision without
        // mutating either the original evidence or an earlier pending preview.
        return matches.Select(match => match with { HasValidatedApprovalReceipt = true }).ToArray();
    }
}
