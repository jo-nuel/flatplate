using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using FlatPlate.App.Services;
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
    private readonly ObservableCollection<PriceBookPendingDraft> _pendingDrafts = new();
    private string _operationMessage = "Prices have not been loaded. Saving is not available yet.";
    private bool _isOperationError;
    private PriceBookCoordinator? _actions;
    private IReadOnlyList<string> _storeOptions = new[] { AllStoresOption, "Woolworths", "Coles" };
    private PriceCsvImportPreviewViewModel? _lastImportPreview;
    private bool _isSavingAvailable;

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
        PendingDrafts = new ReadOnlyObservableCollection<PriceBookPendingDraft>(_pendingDrafts);
        _rows.CollectionChanged += (_, _) => RaiseEmptyStateChanged();
        VisiblePrices.CollectionChanged += (_, _) => RaiseEmptyStateChanged();
    }

    public IReadOnlyList<string> StoreOptions => _storeOptions;
    public PriceBookCoordinator? Actions => _actions;
    public ReadOnlyObservableCollection<PriceBookPendingDraft> PendingDrafts { get; }
    public bool HasPendingDrafts => _pendingDrafts.Count > 0;
    public string OperationMessage => _operationMessage;
    public bool IsOperationError => _isOperationError;
    public bool IsSavingAvailable => _isSavingAvailable;
    public PriceCsvImportPreviewViewModel? LastImportPreview => _lastImportPreview;
    public string EmptyTitle => _rows.Count == 0 ? "No prices yet" : "No prices for this store";
    public string EmptyDescription => _rows.Count == 0
        ? IsSavingAvailable ? "Add a price or import a valid CSV to start the book." : "Prepare a draft or preview a CSV to review prices. Saving is not connected yet."
        : "Choose another store or All stores to see available prices.";

    internal void ConnectActions(PriceBookCoordinator actions)
    {
        if (_actions is not null) throw new InvalidOperationException("The Price Book already has an interaction coordinator.");
        _actions = actions;
        OnPropertyChanged(nameof(Actions));
    }

    internal void DisconnectActions(PriceBookCoordinator actions)
    {
        if (ReferenceEquals(_actions, actions))
        {
            _actions = null;
            OnPropertyChanged(nameof(Actions));
        }
    }

    internal void SetStores(IEnumerable<Store> stores)
    {
        _storeOptions = new[] { AllStoresOption }.Concat(stores.Select(s => s.Name).Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)).ToArray();
        OnPropertyChanged(nameof(StoreOptions));
        if (!_storeOptions.Contains(SelectedStore)) SelectedStore = AllStoresOption;
    }

    internal void RetainDraft(PriceBookPendingDraft draft)
    {
        _pendingDrafts.Add(draft);
        OnPropertyChanged(nameof(HasPendingDrafts));
    }

    internal void ReleaseDraft(PriceBookPendingDraft draft)
    {
        _pendingDrafts.Remove(draft);
        OnPropertyChanged(nameof(HasPendingDrafts));
    }

    internal void SetSavingAvailable(bool available)
    {
        _isSavingAvailable = available;
        OnPropertyChanged(nameof(IsSavingAvailable));
        OnPropertyChanged(nameof(EmptyDescription));
    }

    internal void SetMessage(string message, bool isError = false)
    {
        _operationMessage = message;
        _isOperationError = isError;
        OnPropertyChanged(nameof(OperationMessage));
        OnPropertyChanged(nameof(IsOperationError));
    }

    internal void SetPreview(PriceCsvImportPreviewViewModel preview)
    {
        _lastImportPreview = preview;
        OnPropertyChanged(nameof(LastImportPreview));
    }

    private void RaiseEmptyStateChanged()
    {
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
    }

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
