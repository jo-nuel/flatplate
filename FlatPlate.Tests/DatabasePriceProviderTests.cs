using FlatPlate.Core.Data;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that the database price provider returns the correct store prices.
/// </summary>
public sealed class DatabasePriceProviderTests
{
    private SqliteConnection _connection = null!;
    private FlatPlateDbContext _context = null!;
    private DatabasePriceProvider _provider = null!;

    /// <summary>
    /// Creates an isolated in-memory SQLite database for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FlatPlateDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new FlatPlateDbContext(options);
        _context.Database.EnsureCreated();
        _provider = new DatabasePriceProvider(_context);
    }

    /// <summary>
    /// Closes the temporary database after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Confirms that an existing ingredient and store pair returns its price.
    /// </summary>
    [Test]
    public void GetPrice_ExistingPrice_ReturnsPriceEntry()
    {
        var (ingredient, store) = AddPrice("Rice", "Woolworths", 4.50m, 1000m);

        var result = _provider.GetPrice(ingredient.Id, store.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Price, Is.EqualTo(4.50m));
            Assert.That(result.PackSize, Is.EqualTo(1000m));
            Assert.That(result.Source, Is.EqualTo(PriceSource.Seeded));
        });
    }

    /// <summary>
    /// Confirms that a missing ingredient and store pair returns null.
    /// </summary>
    [Test]
    public void GetPrice_MissingPrice_ReturnsNull()
    {
        var result = _provider.GetPrice(1, 1);

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Confirms that store prices are filtered and ordered by ingredient.
    /// </summary>
    [Test]
    public void GetPricesForStore_MultipleStores_ReturnsSelectedStorePrices()
    {
        var (_, woolworths) = AddPrice("Rice", "Woolworths", 4.50m, 1000m);
        AddPrice("Milk", "Coles", 3.20m, 2000m);
        AddPrice("Chicken breast", woolworths, 12.00m, 1000m);

        var result = _provider.GetPricesForStore(woolworths.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(entry => entry.StoreId),
                Is.All.EqualTo(woolworths.Id));
            Assert.That(result.Select(entry => entry.IngredientId),
                Is.Ordered.Ascending);
        });
    }

    /// <summary>
    /// Confirms that invalid identifiers are rejected before querying SQLite.
    /// </summary>
    [TestCase(0, 1)]
    [TestCase(1, 0)]
    [TestCase(-1, 1)]
    public void GetPrice_InvalidIdentifier_Throws(int ingredientId, int storeId)
    {
        Assert.That(
            () => _provider.GetPrice(ingredientId, storeId),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    /// <summary>
    /// Confirms that an invalid store identifier is rejected.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void GetPricesForStore_InvalidStoreId_Throws(int storeId)
    {
        Assert.That(
            () => _provider.GetPricesForStore(storeId),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private (Ingredient Ingredient, Store Store) AddPrice(
        string ingredientName,
        string storeName,
        decimal price,
        decimal packSize)
    {
        var store = new Store { Name = storeName };
        return AddPrice(ingredientName, store, price, packSize);
    }

    private (Ingredient Ingredient, Store Store) AddPrice(
        string ingredientName,
        Store store,
        decimal price,
        decimal packSize)
    {
        if (store.Id > 0)
        {
            _context.Stores.Attach(store);
        }

        var ingredient = new WeightIngredient { Name = ingredientName };
        var priceEntry = new PriceEntry
        {
            Ingredient = ingredient,
            Store = store,
            Price = price,
            PackSize = packSize,
            LastUpdated = new DateTime(2026, 9, 17),
            Source = PriceSource.Seeded,
        };

        _context.PriceEntries.Add(priceEntry);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        return (ingredient, store);
    }
}
