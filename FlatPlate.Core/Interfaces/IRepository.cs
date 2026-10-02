namespace FlatPlate.Core.Interfaces;

/// <summary>
/// Defines the common database operations used by FlatPlate services.
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets every stored item of this type.
    /// </summary>
    IReadOnlyList<T> GetAll();

    /// <summary>
    /// Gets one item by its database identifier, or null when it does not exist.
    /// </summary>
    T? GetById(int id);

    /// <summary>
    /// Gets items that match the supplied rule.
    /// </summary>
    IReadOnlyList<T> Find(Func<T, bool> predicate);

    /// <summary>
    /// Adds a new item to storage.
    /// </summary>
    void Add(T entity);

    /// <summary>
    /// Saves changes to an existing item.
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// Removes an item from storage.
    /// </summary>
    void Delete(T entity);
}
