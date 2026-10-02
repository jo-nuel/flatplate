namespace FlatPlate.Core.Models;

/// <summary>
/// Summarises weekly cost and any ingredients missing a store price.
/// </summary>
public sealed class BudgetResult
{
    public decimal Budget { get; init; }

    public decimal TotalCost { get; init; }

    public decimal PercentageUsed { get; init; }

    public bool IsOverBudget { get; init; }

    public IReadOnlyList<MergedItem> MissingPriceItems { get; init; } = Array.Empty<MergedItem>();
}
