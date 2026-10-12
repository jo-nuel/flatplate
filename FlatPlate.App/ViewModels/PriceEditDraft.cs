using FlatPlate.Core.Enums;

namespace FlatPlate.App.ViewModels;

/// <summary>Validated unsaved price values; persistence is handled separately.</summary>
public sealed class PriceEditDraft
{
    public required PriceEditMode Mode { get; init; }

    public required int IngredientId { get; init; }

    public required int StoreId { get; init; }

    /// <summary>Package price in AUD.</summary>
    public required decimal Price { get; init; }

    /// <summary>Pack size in the ingredient's base unit.</summary>
    public required decimal PackSize { get; init; }

    /// <summary>The base unit the pack size was entered in: g, mL or pieces.</summary>
    public required string BaseUnit { get; init; }

    /// <summary>Provenance of the confirmed values. Always UserEntered, because the user typed them.</summary>
    public required PriceSource Source { get; init; }

    /// <summary>
    /// When the user confirmed the values. Whether this becomes the entry's LastUpdated
    /// is decided by the persistence operation, not by the dialog.
    /// </summary>
    public required DateTime EnteredAt { get; init; }

    /// <summary>Edit only: the source of the record as it was before editing.</summary>
    public PriceSource? OriginalSource { get; init; }

    /// <summary>Edit only: the LastUpdated value of the record as it was before editing.</summary>
    public DateTime? OriginalLastUpdated { get; init; }
}
