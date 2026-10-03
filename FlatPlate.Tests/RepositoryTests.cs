using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that the generic repository persists and retrieves entities correctly.
/// </summary>
public sealed class RepositoryTests
{
    private SqliteConnection _connection = null!;
    private FlatPlateDbContext _context = null!;
    private Repository<Store> _repository = null!;

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
        _repository = new Repository<Store>(_context);
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
    /// Confirms that adding a store saves it to SQLite.
    /// </summary>
    [Test]
    public void Add_NewStore_PersistsStore()
    {
        _repository.Add(new Store { Name = "Woolworths" });

        var savedStore = _context.Stores.Single();

        Assert.That(savedStore.Name, Is.EqualTo("Woolworths"));
    }

    /// <summary>
    /// Confirms that an existing store can be retrieved by its identifier.
    /// </summary>
    [Test]
    public void GetById_ExistingStore_ReturnsStore()
    {
        var store = AddStore("Coles");

        var result = _repository.GetById(store.Id);

        Assert.That(result, Is.SameAs(store));
    }

    /// <summary>
    /// Confirms that an unknown identifier returns null.
    /// </summary>
    [Test]
    public void GetById_MissingStore_ReturnsNull()
    {
        var result = _repository.GetById(999);

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Confirms that all stored entities are returned.
    /// </summary>
    [Test]
    public void GetAll_MultipleStores_ReturnsAllStores()
    {
        AddStore("Woolworths");
        AddStore("Coles");

        var result = _repository.GetAll();

        Assert.That(result, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Confirms that a lambda predicate filters stored entities.
    /// </summary>
    [Test]
    public void Find_MatchingPredicate_ReturnsMatches()
    {
        AddStore("Woolworths");
        AddStore("Coles");

        var result = _repository.Find(store => store.Name.StartsWith('W'));

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Woolworths"));
    }

    /// <summary>
    /// Confirms that updating an entity persists its changed values.
    /// </summary>
    [Test]
    public void Update_ExistingStore_PersistsChanges()
    {
        var store = AddStore("Aldi");
        store.Name = "ALDI";

        _repository.Update(store);
        _context.ChangeTracker.Clear();

        Assert.That(_context.Stores.Find(store.Id)?.Name, Is.EqualTo("ALDI"));
    }

    /// <summary>
    /// Confirms that deleting an entity removes it from SQLite.
    /// </summary>
    [Test]
    public void Delete_ExistingStore_RemovesStore()
    {
        var store = AddStore("Coles");

        _repository.Delete(store);

        Assert.That(_context.Stores, Is.Empty);
    }

    private Store AddStore(string name)
    {
        var store = new Store { Name = name };
        _repository.Add(store);
        return store;
    }
}
