using System.Collections;
using System.IO;
using FlatPlate.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FlatPlate.App.Services;

/// <summary>Owns only a bounded write transaction, never the shared context lifetime.</summary>
internal static class AppWriteTransaction
{
    public static T Execute<T>(FlatPlateDbContext context, Func<T> apply)
    {
        context.ChangeTracker.DetectChanges();
        if (context.Database.CurrentTransaction is not null || context.ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidDataException("Another operation has pending changes. Finish it and refresh before saving.");

        var snapshots = context.ChangeTracker.Entries().Select(e => new Snapshot(e.Entity,
            e.CurrentValues.Clone(), e.OriginalValues.Clone(), e.State,
            e.Navigations.Select(n => new Navigation(n.Metadata.Name, n.CurrentValue,
                n.Metadata.IsCollection && n.CurrentValue is IEnumerable values ? values.Cast<object>().ToArray() : null)).ToArray())).ToArray();

        using var transaction = context.Database.BeginTransaction();
        T result;
        try
        {
            result = apply();
            context.SaveChanges(acceptAllChangesOnSuccess: false);
            transaction.Commit();
        }
        catch
        {
            try { transaction.Rollback(); }
            finally
            {
                foreach (var entry in context.ChangeTracker.Entries().ToArray())
                    if (!snapshots.Any(s => ReferenceEquals(s.Entity, entry.Entity))) entry.State = EntityState.Detached;
                foreach (var snapshot in snapshots)
                {
                    var entry = context.Entry(snapshot.Entity);
                    entry.State = EntityState.Unchanged;
                    entry.CurrentValues.SetValues(snapshot.Current);
                    entry.OriginalValues.SetValues(snapshot.Original);
                    entry.State = snapshot.State;
                }
                // Restore relationship collections as well as scalar values after EF fixup.
                foreach (var snapshot in snapshots)
                    foreach (var navigation in snapshot.Navigations)
                    {
                        if (navigation.Value is IList list && navigation.Items is not null)
                        {
                            list.Clear(); foreach (var item in navigation.Items) list.Add(item);
                        }
                        else context.Entry(snapshot.Entity).Navigation(navigation.Name).CurrentValue = navigation.Value;
                    }
            }
            throw;
        }
        context.ChangeTracker.AcceptAllChanges();
        return result;
    }

    private sealed record Navigation(string Name, object? Value, object[]? Items);
    private sealed record Snapshot(object Entity, PropertyValues Current, PropertyValues Original,
        EntityState State, Navigation[] Navigations);
}
