using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;

namespace FlatPlate.Core.Services;

/// <summary>
/// Calculates the whole packs needed for a shopping list at one store.
/// </summary>
public sealed class BudgetCalculator : IBudgetCalculator
{
    private readonly IPriceProvider _priceProvider;

    /// <summary>
    /// Creates a calculator that reads prices from the supplied provider.
    /// </summary>
    public BudgetCalculator(IPriceProvider priceProvider)
    {
        ArgumentNullException.ThrowIfNull(priceProvider);
        _priceProvider = priceProvider;
    }

    /// <inheritdoc />
    public BudgetResult Calculate(
        IEnumerable<MergedItem> items,
        int storeId,
        decimal budget)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storeId);

        if (budget <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(budget),
                budget,
                "Weekly budget must be greater than zero.");
        }

        var missingPriceItems = new List<MergedItem>();
        var totalCost = 0m;

        foreach (var item in items)
        {
            ValidateItem(item);
            var priceEntry = _priceProvider.GetPrice(item.Ingredient.Id, storeId);

            if (priceEntry is null)
            {
                missingPriceItems.Add(item);
                continue;
            }

            totalCost += CalculateItemCost(item, priceEntry);
        }

        return new BudgetResult
        {
            Budget = budget,
            TotalCost = totalCost,
            PercentageUsed = totalCost / budget * 100m,
            IsOverBudget = totalCost > budget,
            MissingPriceItems = missingPriceItems,
        };
    }

    private static decimal CalculateItemCost(
        MergedItem item,
        PriceEntry priceEntry)
    {
        if (priceEntry.PackSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priceEntry.PackSize),
                priceEntry.PackSize,
                "Price pack size must be greater than zero.");
        }

        if (priceEntry.Price < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priceEntry.Price),
                priceEntry.Price,
                "Price cannot be negative.");
        }

        // Shops sell whole packs, so a partial pack must be rounded up.
        var packsNeeded = Math.Ceiling(item.BaseAmount / priceEntry.PackSize);
        return packsNeeded * priceEntry.Price;
    }

    private static void ValidateItem(MergedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.BaseAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(item.BaseAmount),
                item.BaseAmount,
                "Ingredient amount must be greater than zero.");
        }
    }
}
