using FlatPlate.Core.Models;

namespace FlatPlate.App.Services;

/// <summary>Read operations supplied by the host; this type owns no context or database path.</summary>
public sealed class PriceBookDataSource
{
    public PriceBookDataSource(
        Func<IReadOnlyList<PriceEntry>> loadPrices,
        Func<IReadOnlyList<Ingredient>> loadIngredients,
        Func<IReadOnlyList<Store>> loadStores)
    {
        LoadPrices = loadPrices ?? throw new ArgumentNullException(nameof(loadPrices));
        LoadIngredients = loadIngredients ?? throw new ArgumentNullException(nameof(loadIngredients));
        LoadStores = loadStores ?? throw new ArgumentNullException(nameof(loadStores));
    }

    public PriceBookDataSource(PriceBookReadService reader,
        Func<IReadOnlyList<Ingredient>> loadIngredients, Func<IReadOnlyList<Store>> loadStores)
        : this((reader ?? throw new ArgumentNullException(nameof(reader))).LoadAllCurrentPrices, loadIngredients, loadStores)
    {
    }

    public Func<IReadOnlyList<PriceEntry>> LoadPrices { get; }
    public Func<IReadOnlyList<Ingredient>> LoadIngredients { get; }
    public Func<IReadOnlyList<Store>> LoadStores { get; }
}
