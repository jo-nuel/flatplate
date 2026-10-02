namespace FlatPlate.Core.Models;

/// <summary>
/// Represents a supermarket whose prices can be selected by the planner.
/// </summary>
public sealed class Store
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<PriceEntry> Prices { get; set; } = new List<PriceEntry>();
}
