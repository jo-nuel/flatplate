using System.IO;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Manual current-price insert/update using the existing composite identity.</summary>
public sealed class PriceBookWriteService
{
    private readonly Func<DateTime> _timestamp;
    internal FlatPlateDbContext Context { get; }

    /// <summary>The host supplies the explicitly agreed save-time policy; no timezone is inferred.</summary>
    public PriceBookWriteService(FlatPlateDbContext context, Func<DateTime> timestamp)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _timestamp = timestamp ?? throw new ArgumentNullException(nameof(timestamp));
    }

    public void Save(PriceEditDraft draft, PriceBookRowViewModel? expectedOriginal = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!Enum.IsDefined(draft.Mode) || draft.IngredientId <= 0 || draft.StoreId <= 0
            || draft.Price <= 0m || draft.PackSize <= 0m || draft.Source != PriceSource.UserEntered)
            throw new InvalidDataException("The price draft is invalid. Correct it before saving.");
        if (draft.Mode == PriceEditMode.Edit && (expectedOriginal is null
            || expectedOriginal.IngredientId != draft.IngredientId || expectedOriginal.StoreId != draft.StoreId
            || draft.OriginalSource != expectedOriginal.Source || draft.OriginalLastUpdated != expectedOriginal.LastUpdated))
            throw new InvalidDataException("The price identity or original observation changed. Refresh before editing.");
        if (draft.Mode == PriceEditMode.Add && expectedOriginal is not null)
            throw new InvalidDataException("A new price cannot replace an existing record.");

        AppWriteTransaction.Execute(Context, () =>
        {
            var ingredient = Context.Ingredients.AsNoTracking().SingleOrDefault(i => i.Id == draft.IngredientId);
            if (ingredient is null || !Context.Stores.AsNoTracking().Any(s => s.Id == draft.StoreId)
                || PriceBookRowViewModel.GetBaseUnit(ingredient) is not string unit || unit != draft.BaseUnit)
                throw new InvalidDataException("The ingredient, store or base unit is no longer valid. Refresh the catalogue.");

            var entry = Context.PriceEntries.SingleOrDefault(p => p.IngredientId == draft.IngredientId && p.StoreId == draft.StoreId);
            if (entry is not null)
            {
                Context.Entry(entry).Reload();
                if (Context.Entry(entry).State == EntityState.Detached) entry = null;
            }
            if (draft.Mode == PriceEditMode.Add)
            {
                if (entry is not null) throw new InvalidDataException("This ingredient and store already have a price. Use Edit instead.");
                entry = new PriceEntry { IngredientId = draft.IngredientId, StoreId = draft.StoreId };
                Context.PriceEntries.Add(entry);
            }
            else
            {
                if (entry is null) throw new InvalidDataException("This price no longer exists. Refresh before editing.");
                if (entry.Price != expectedOriginal!.Price || entry.PackSize != expectedOriginal.PackSize
                    || entry.Source != expectedOriginal.Source || entry.LastUpdated != expectedOriginal.LastUpdated)
                    throw new InvalidDataException("This price changed since the dialog opened. Refresh and review the new value.");
                if (entry.Price == draft.Price && entry.PackSize == draft.PackSize)
                    throw new InvalidDataException("Change a price value before saving.");
            }
            var timestamp = _timestamp();
            if (timestamp.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("The agreed manual save clock must return UTC time.");
            entry.Price = draft.Price; entry.PackSize = draft.PackSize;
            entry.Source = PriceSource.UserEntered; entry.LastUpdated = timestamp;
            return true;
        });
    }
}
