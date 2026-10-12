using System.Globalization;
using System.Runtime.CompilerServices;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

/// <summary>Validates price input and returns a draft without changing the original entry.</summary>
public sealed class PriceEditDialogViewModel : ViewModelBase
{
    private const string IngredientField = "Ingredient";
    private const string StoreField = "Store";
    private const string PriceField = "Price";
    private const string PackSizeField = "PackSize";

    private readonly HashSet<(int IngredientId, int StoreId)> _existingPriceKeys;
    private readonly PriceBookRowViewModel? _original;
    private readonly Func<DateTime> _clock;
    private readonly HashSet<string> _editedFields = new();

    private Ingredient? _selectedIngredient;
    private Store? _selectedStore;
    private string _priceText = string.Empty;
    private string _packSizeText = string.Empty;
    private bool _confirmAttempted;
    private PriceEditDraft? _result;
    private bool _saveOnConfirm;

    private PriceEditDialogViewModel(
        PriceEditMode mode,
        IReadOnlyList<Ingredient> ingredientOptions,
        IReadOnlyList<Store> storeOptions,
        IEnumerable<(int IngredientId, int StoreId)> existingPriceKeys,
        PriceBookRowViewModel? original,
        Func<DateTime>? clock)
    {
        ArgumentNullException.ThrowIfNull(ingredientOptions);
        ArgumentNullException.ThrowIfNull(storeOptions);
        ArgumentNullException.ThrowIfNull(existingPriceKeys);

        if (ingredientOptions.Any(ingredient => ingredient is null)
            || storeOptions.Any(store => store is null))
        {
            throw new ArgumentException("Ingredient and store options cannot contain null items.");
        }

        Mode = mode;
        IngredientOptions = ingredientOptions
            .OrderBy(ingredient => ingredient.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        StoreOptions = storeOptions
            .OrderBy(store => store.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _existingPriceKeys = existingPriceKeys.ToHashSet();
        _original = original;
        _clock = clock ?? (() => DateTime.Now);
        ConfirmCommand = new RelayCommand(Confirm);

        if (original is not null)
        {
            _selectedIngredient = IngredientOptions.FirstOrDefault(
                ingredient => ingredient.Id == original.IngredientId);
            _selectedStore = StoreOptions.FirstOrDefault(
                store => store.Id == original.StoreId);
            _priceText = original.Price.ToString(CultureInfo.InvariantCulture);
            _packSizeText = original.PackSize.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Creates the dialog for a new price, choosing from the supplied ingredients and stores.</summary>
    public static PriceEditDialogViewModel ForAdd(
        IReadOnlyList<Ingredient> ingredientOptions,
        IReadOnlyList<Store> storeOptions,
        IEnumerable<(int IngredientId, int StoreId)> existingPriceKeys,
        Func<DateTime>? clock = null)
    {
        return new PriceEditDialogViewModel(
            PriceEditMode.Add,
            ingredientOptions,
            storeOptions,
            existingPriceKeys,
            original: null,
            clock);
    }

    /// <summary>Creates the dialog for an existing price, starting from the selected row's values.</summary>
    public static PriceEditDialogViewModel ForEdit(
        PriceBookRowViewModel selectedPrice,
        IReadOnlyList<Ingredient> ingredientOptions,
        IReadOnlyList<Store> storeOptions,
        Func<DateTime>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(selectedPrice);

        return new PriceEditDialogViewModel(
            PriceEditMode.Edit,
            ingredientOptions,
            storeOptions,
            Array.Empty<(int, int)>(),
            selectedPrice,
            clock);
    }

    /// <summary>Raised after Confirm produced a valid draft, so the dialog can close.</summary>
    public event EventHandler? Confirmed;

    public PriceEditMode Mode { get; }

    public bool IsEditMode => Mode == PriceEditMode.Edit;

    /// <summary>Ingredient and store identify a price, so only Add lets the user choose them.</summary>
    public bool CanChangeRecord => !IsEditMode;

    public string Title => IsEditMode ? "Edit Price" : "Add Price";
    public bool SaveOnConfirm
    {
        get => _saveOnConfirm;
        set { if (SetProperty(ref _saveOnConfirm, value)) OnPropertyChanged(nameof(ConfirmLabel)); }
    }
    public string ConfirmLabel => SaveOnConfirm ? "Save Price" : "Confirm";

    public string Description => IsEditMode
        ? "Update the pack price or pack size. The ingredient and store identify this price and cannot be changed here."
        : "Enter the supermarket pack price and pack size for one ingredient.";

    public string ProvenanceNote => _original is null
        ? "New prices are marked as UserEntered."
        : string.Format(
            CultureInfo.InvariantCulture,
            "Current price: {0}, last updated {1:dd MMM yyyy}. Confirmed changes are marked as UserEntered.",
            _original.Source,
            _original.LastUpdated);

    public IReadOnlyList<Ingredient> IngredientOptions { get; }

    public IReadOnlyList<Store> StoreOptions { get; }

    public RelayCommand ConfirmCommand { get; }

    /// <summary>The validated draft after a successful Confirm; null otherwise, including after Cancel.</summary>
    public PriceEditDraft? Result
    {
        get => _result;
        private set => SetProperty(ref _result, value);
    }

    public Ingredient? SelectedIngredient
    {
        get => _selectedIngredient;
        set
        {
            if (CanChangeRecord)
            {
                SetInput(ref _selectedIngredient, value, IngredientField);
            }
        }
    }

    public Store? SelectedStore
    {
        get => _selectedStore;
        set
        {
            if (CanChangeRecord)
            {
                SetInput(ref _selectedStore, value, StoreField);
            }
        }
    }

    public string PriceText
    {
        get => _priceText;
        set => SetInput(ref _priceText, value ?? string.Empty, PriceField);
    }

    public string PackSizeText
    {
        get => _packSizeText;
        set => SetInput(ref _packSizeText, value ?? string.Empty, PackSizeField);
    }

    /// <summary>The base unit of the selected ingredient's subtype, shown next to the pack size.</summary>
    public string BaseUnitDisplay => SelectedIngredient is null
        ? "—"
        : PriceBookRowViewModel.GetBaseUnit(SelectedIngredient) ?? "unknown unit";

    public string IngredientError => Shown(IngredientField, GetIngredientError());

    public string StoreError => Shown(StoreField, GetStoreError());

    public string PriceError => Shown(PriceField, GetPriceError());

    public string PackSizeError => Shown(PackSizeField, GetPackSizeError());

    /// <summary>Problems with the combination of fields; a duplicate Add is shown as soon as it is known.</summary>
    public string FormError
    {
        get
        {
            var error = GetFormError();
            return error is not null && (_confirmAttempted || IsDuplicateAdd())
                ? error
                : string.Empty;
        }
    }

    /// <summary>True when every field and the field combination are valid.</summary>
    public bool IsValid =>
        GetIngredientError() is null
        && GetStoreError() is null
        && GetPriceError() is null
        && GetPackSizeError() is null
        && GetFormError() is null;

    private void Confirm()
    {
        _confirmAttempted = true;
        RaiseValidationChanged();

        if (!IsValid)
        {
            return;
        }

        Result = new PriceEditDraft
        {
            Mode = Mode,
            IngredientId = SelectedIngredient!.Id,
            StoreId = SelectedStore!.Id,
            Price = ParseAmount(PriceText)!.Value,
            PackSize = ParseAmount(PackSizeText)!.Value,
            BaseUnit = PriceBookRowViewModel.GetBaseUnit(SelectedIngredient)!,
            Source = PriceSource.UserEntered,
            EnteredAt = _clock(),
            OriginalSource = _original?.Source,
            OriginalLastUpdated = _original?.LastUpdated,
        };

        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    private void SetInput<T>(
        ref T field,
        T value,
        string fieldName,
        [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            _editedFields.Add(fieldName);
            RaiseValidationChanged();
        }
    }

    private void RaiseValidationChanged()
    {
        OnPropertyChanged(nameof(BaseUnitDisplay));
        OnPropertyChanged(nameof(IngredientError));
        OnPropertyChanged(nameof(StoreError));
        OnPropertyChanged(nameof(PriceError));
        OnPropertyChanged(nameof(PackSizeError));
        OnPropertyChanged(nameof(FormError));
        OnPropertyChanged(nameof(IsValid));
    }

    private string Shown(string fieldName, string? error)
    {
        return error is not null && (_confirmAttempted || _editedFields.Contains(fieldName))
            ? error
            : string.Empty;
    }

    private string? GetIngredientError()
    {
        if (SelectedIngredient is null)
        {
            return _original is null
                ? "Select an ingredient."
                : $"The ingredient of this price (id {_original.IngredientId}) is not in the available list.";
        }

        if (!IngredientOptions.Contains(SelectedIngredient))
        {
            return "Select an ingredient from the list.";
        }

        return PriceBookRowViewModel.GetBaseUnit(SelectedIngredient) is null
            ? "This ingredient has no known base unit, so its pack size cannot be entered."
            : null;
    }

    private string? GetStoreError()
    {
        if (SelectedStore is null)
        {
            return _original is null
                ? "Select a store."
                : $"The store of this price (id {_original.StoreId}) is not in the available list.";
        }

        return StoreOptions.Contains(SelectedStore) ? null : "Select a store from the list.";
    }

    private string? GetPriceError()
    {
        if (string.IsNullOrWhiteSpace(PriceText))
        {
            return "Enter the package price in AUD.";
        }

        var price = ParseAmount(PriceText);

        if (price is null)
        {
            return "Enter a decimal price without separators or excess precision, for example 3.50.";
        }

        if (price <= 0m)
        {
            return "The price must be more than 0.";
        }

        return null;
    }

    private string? GetPackSizeError()
    {
        var unit = SelectedIngredient is null ? null : PriceBookRowViewModel.GetBaseUnit(SelectedIngredient);
        var unitText = unit ?? "the ingredient's base unit";

        if (string.IsNullOrWhiteSpace(PackSizeText))
        {
            return $"Enter the pack size in {unitText}.";
        }

        var packSize = ParseAmount(PackSizeText);

        if (packSize is null)
        {
            return $"Enter a decimal pack size in {unitText} without separators or excess precision, for example 500.";
        }

        if (packSize <= 0m)
        {
            return "The pack size must be more than 0.";
        }

        return null;
    }

    private string? GetFormError()
    {
        if (SelectedIngredient is null || SelectedStore is null)
        {
            return null;
        }

        if (IsDuplicateAdd())
        {
            return $"{SelectedStore.Name} already has a price for {SelectedIngredient.Name}. Use Edit Price to change it.";
        }

        if (_original is null)
        {
            return null;
        }

        if (SelectedIngredient.Id != _original.IngredientId || SelectedStore.Id != _original.StoreId)
        {
            return "The ingredient and store of an existing price cannot be changed.";
        }

        var price = ParseAmount(PriceText);
        var packSize = ParseAmount(PackSizeText);

        return price == _original.Price && packSize == _original.PackSize
            ? "Change the price or pack size before confirming."
            : null;
    }

    private bool IsDuplicateAdd()
    {
        return _original is null
            && SelectedIngredient is not null
            && SelectedStore is not null
            && _existingPriceKeys.Contains((SelectedIngredient.Id, SelectedStore.Id));
    }

    private static decimal? ParseAmount(string text)
    {
        text = text.Trim();
        if (!decimal.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var value))
        {
            return null;
        }

        // TryParse can succeed by rounding. Compare exact numeric text, as CSV V1 does,
        // ignoring only equivalent signs/leading zeros and insignificant trailing zeros.
        return NormaliseDecimal(text) == NormaliseDecimal(value.ToString(CultureInfo.InvariantCulture))
            ? value
            : null;
    }

    private static string NormaliseDecimal(string text)
    {
        var negative = text.StartsWith('-');
        if (text.StartsWith('+') || negative)
        {
            text = text[1..];
        }

        var parts = text.Split('.');
        var integer = parts[0].TrimStart('0');
        if (integer.Length == 0)
        {
            integer = "0";
        }

        var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : string.Empty;
        var number = fraction.Length == 0 ? integer : integer + "." + fraction;
        return negative && number != "0" ? "-" + number : number;
    }
}
