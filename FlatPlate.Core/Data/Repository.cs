using FlatPlate.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Core.Data;

/// <summary>
/// Provides the standard create, read, update, and delete operations for an EF Core entity.
/// </summary>
public sealed class Repository<T> : IRepository<T> where T : class
{
    private readonly FlatPlateDbContext _context;
    private readonly DbSet<T> _entities;

    /// <summary>
    /// Creates a repository that saves changes through the supplied database context.
    /// </summary>
    public Repository(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _entities = context.Set<T>();
    }

    /// <inheritdoc />
    public IReadOnlyList<T> GetAll()
    {
        return _entities.ToList();
    }

    /// <inheritdoc />
    public T? GetById(int id)
    {
        return _entities.Find(id);
    }

    /// <inheritdoc />
    public IReadOnlyList<T> Find(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return _entities.Where(predicate).ToList();
    }

    /// <inheritdoc />
    public void Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _entities.Add(entity);
        _context.SaveChanges();
    }

    /// <inheritdoc />
    public void Update(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _entities.Update(entity);
        _context.SaveChanges();
    }

    /// <inheritdoc />
    public void Delete(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _entities.Remove(entity);
        _context.SaveChanges();
    }
}
