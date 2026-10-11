using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Holds the supplied price rows for the Price Book and filters them by supermarket.
/// </summary>
public sealed class PriceBookViewModel : ViewModelBase
{
    public const string AllStoresOption = "All stores";

    private readonly ObservableCollection<PriceBookRowViewModel> _rows = new();

    private string _selectedStore = AllStoresOption;
    private PriceBookRowViewModel? _selectedPrice;

    /// <summary>
    /// Creates an empty Price Book showing all stores.
    /// </summary>
    public PriceBookViewModel()
    {
        Prices = new ReadOnlyObservableCollection<PriceBookRowViewModel>(_rows);
        VisiblePrices = new ListCollectionView(_rows)
        {
            Filter = IsVisibleForSelectedStore,
        };
    }

    public IReadOnlyList<string> StoreOptions { get; } =
        new[] { AllStoresOption, "Woolworths", "Coles" };

    /// <summary>
    /// Every loaded row, regardless of the store filter.
    /// </summary>
    public ReadOnlyObservableCollection<PriceBookRowViewModel> Prices { get; }

    /// <summary>
    /// The rows that match the selected store; the table binds to this view.
    /// </summary>
    public ICollectionView VisiblePrices { get; }

    public string SelectedStore
    {
        get => _selectedStore;
        set
        {
            if (SetProperty(ref _selectedStore, value ?? AllStoresOption))
            {
                RefreshVisiblePrices();
            }
        }
    }

    public PriceBookRowViewModel? SelectedPrice
    {
        get => _selectedPrice;
        set => SetProperty(ref _selectedPrice, value);
    }

    /// <summary>
    /// Replaces the table rows with the supplied prices, ordered by ingredient then store.
    /// </summary>
    public void LoadPrices(IEnumerable<PriceEntry> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        // Build every row first so an invalid entry leaves the current rows untouched.
        var rows = prices
            .Select(entry => new PriceBookRowViewModel(entry))
            .OrderBy(row => row.IngredientName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.StoreName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        SelectedPrice = null;
        _rows.Clear();

        foreach (var row in rows)
        {
            _rows.Add(row);
        }
    }

    private bool IsVisibleForSelectedStore(object item)
    {
        return SelectedStore == AllStoresOption
            || (item is PriceBookRowViewModel row
                && string.Equals(row.StoreName, SelectedStore, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshVisiblePrices()
    {
        VisiblePrices.Refresh();

        if (SelectedPrice is not null && !VisiblePrices.Contains(SelectedPrice))
        {
            SelectedPrice = null;
        }
    }
}
