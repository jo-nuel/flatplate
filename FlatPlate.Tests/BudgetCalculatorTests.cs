using FlatPlate.Core.Enums;
using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that shopping-list quantities are costed as whole supermarket packs.
/// </summary>
public sealed class BudgetCalculatorTests
{
    private const int WoolworthsStoreId = 1;

    private TestPriceProvider _priceProvider = null!;
    private BudgetCalculator _calculator = null!;

    /// <summary>
    /// Creates an empty in-memory price provider for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _priceProvider = new TestPriceProvider();
        _calculator = new BudgetCalculator(_priceProvider);
    }

    /// <summary>
    /// Confirms that a partial pack is rounded up to the next whole pack.
    /// </summary>
    [Test]
    public void Calculate_PartialPack_RoundsUpCost()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        _priceProvider.AddPrice(rice, WoolworthsStoreId, 3m, 500m);
        var items = new[] { CreateMergedItem(rice, 1001m) };

        var result = _calculator.Calculate(items, WoolworthsStoreId, 20m);

        Assert.That(result.TotalCost, Is.EqualTo(9m));
    }

    /// <summary>
    /// Confirms that spending the full budget is not treated as over budget.
    /// </summary>
    [Test]
    public void Calculate_CostEqualsBudget_ReportsFullBudgetUsed()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        _priceProvider.AddPrice(rice, WoolworthsStoreId, 5m, 500m);
        var items = new[] { CreateMergedItem(rice, 1000m) };

        var result = _calculator.Calculate(items, WoolworthsStoreId, 10m);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCost, Is.EqualTo(10m));
            Assert.That(result.PercentageUsed, Is.EqualTo(100m));
            Assert.That(result.IsOverBudget, Is.False);
        });
    }

    /// <summary>
    /// Confirms that spending above the limit is reported to the planner.
    /// </summary>
    [Test]
    public void Calculate_CostExceedsBudget_ReportsOverBudget()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        _priceProvider.AddPrice(rice, WoolworthsStoreId, 6m, 500m);
        var items = new[] { CreateMergedItem(rice, 1000m) };

        var result = _calculator.Calculate(items, WoolworthsStoreId, 10m);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCost, Is.EqualTo(12m));
            Assert.That(result.PercentageUsed, Is.EqualTo(120m));
            Assert.That(result.IsOverBudget, Is.True);
        });
    }

    /// <summary>
    /// Confirms that missing prices are listed and excluded from the total.
    /// </summary>
    [Test]
    public void Calculate_MissingPrice_ReportsItemWithoutAddingCost()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var eggs = new CountIngredient { Id = 2, Name = "Eggs" };
        _priceProvider.AddPrice(rice, WoolworthsStoreId, 4m, 500m);
        var items = new[]
        {
            CreateMergedItem(rice, 500m),
            CreateMergedItem(eggs, 6m),
        };

        var result = _calculator.Calculate(items, WoolworthsStoreId, 20m);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCost, Is.EqualTo(4m));
            Assert.That(result.MissingPriceItems, Has.Count.EqualTo(1));
            Assert.That(result.MissingPriceItems[0].Ingredient, Is.SameAs(eggs));
        });
    }

    /// <summary>
    /// Confirms that a weekly budget must be greater than zero.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void Calculate_NonPositiveBudget_ThrowsArgumentOutOfRangeException(
        int budget)
    {
        Assert.That(
            () => _calculator.Calculate(
                Array.Empty<MergedItem>(),
                WoolworthsStoreId,
                budget),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static WeightIngredient CreateWeightIngredient(int id, string name)
    {
        return new WeightIngredient { Id = id, Name = name };
    }

    private static MergedItem CreateMergedItem(
        Ingredient ingredient,
        decimal baseAmount)
    {
        return new MergedItem
        {
            Ingredient = ingredient,
            BaseAmount = baseAmount,
        };
    }

    private sealed class TestPriceProvider : IPriceProvider
    {
        private readonly Dictionary<(int IngredientId, int StoreId), PriceEntry> _prices = new();

        public PriceEntry? GetPrice(int ingredientId, int storeId)
        {
            _prices.TryGetValue((ingredientId, storeId), out var price);
            return price;
        }

        public IReadOnlyList<PriceEntry> GetPricesForStore(int storeId)
        {
            return _prices.Values
                .Where(price => price.StoreId == storeId)
                .ToList();
        }

        public void AddPrice(
            Ingredient ingredient,
            int storeId,
            decimal price,
            decimal packSize)
        {
            _prices[(ingredient.Id, storeId)] = new PriceEntry
            {
                IngredientId = ingredient.Id,
                StoreId = storeId,
                Ingredient = ingredient,
                Store = new Store { Id = storeId, Name = "Woolworths" },
                Price = price,
                PackSize = packSize,
                LastUpdated = new DateTime(2026, 9, 17),
                Source = PriceSource.Seeded,
            };
        }
    }
}
