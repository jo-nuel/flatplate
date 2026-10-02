namespace FlatPlate.Core.Models;

/// <summary>
/// Holds one ingredient total after all planned meals have been combined.
/// </summary>
public sealed class MergedItem
{
    public required Ingredient Ingredient { get; init; }

    public decimal BaseAmount { get; init; }
}
