namespace FlatPlate.Core.Enums;

/// <summary>
/// Records where a supermarket price came from.
/// </summary>
public enum PriceSource
{
    Seeded,
    UserEntered,
    Estimated,
    Scraped
}
