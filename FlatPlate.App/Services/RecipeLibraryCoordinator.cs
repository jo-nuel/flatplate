using System.ComponentModel;
using System.IO;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Services;

/// <summary>Read and draft workflow only; the host retains context ownership.</summary>
public sealed class RecipeLibraryCoordinator : IDisposable
{
    private readonly RecipeLibraryDataSource _data;
    private readonly IRecipeLibraryInteraction _interaction;
    private IReadOnlyList<Ingredient> _ingredients = Array.Empty<Ingredient>();
    private bool _loaded, _disposed;

    public RecipeLibraryCoordinator(RecipeLibraryViewModel viewModel, RecipeLibraryDataSource data, IRecipeLibraryInteraction interaction)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        AddRecipeCommand = new RelayCommand(() => Run(Add, "Could not prepare a recipe draft."), () => Ready && _ingredients.Count > 0);
        EditRecipeCommand = new RelayCommand(() => Run(Edit, "Could not prepare an edit draft."), () => Ready && ViewModel.SelectedRecipe is not null && _ingredients.Count > 0);
        ReloadCommand = new RelayCommand(() => Load(), () => !_disposed);
        ViewModel.ConnectActions(this); ViewModel.PropertyChanged += OnChanged;
    }

    public RecipeLibraryViewModel ViewModel { get; }
    public RelayCommand AddRecipeCommand { get; }
    public RelayCommand EditRecipeCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public Exception? LastError { get; private set; }
    private bool Ready => _loaded && !_disposed;

    public bool Load() => Run(() =>
    {
        var ingredients = _data.LoadIngredients().ToArray();
        if (ingredients.Any(i => i is null || i.Id <= 0 || string.IsNullOrWhiteSpace(i.Name)) || ingredients.GroupBy(i => i.Id).Any(g => g.Count() > 1))
            throw new InvalidDataException("The ingredient catalogue is incomplete. Recipes could not be refreshed.");
        ViewModel.LoadRecipes(_data.LoadRecipes()); _ingredients = ingredients; _loaded = true;
        ViewModel.SetStatus("Recipes loaded. Add/Edit prepares drafts; saving is not connected.");
    }, "Could not load recipes. Any previously loaded recipes are unchanged.");

    private void Add()
    {
        if (Load()) Open(RecipeEditDialogViewModel.ForAdd(_ingredients));
    }
    private void Edit()
    {
        var id = ViewModel.SelectedRecipe?.Id;
        if (id is null || !Load()) return;
        var selected = ViewModel.SelectedRecipe;
        if (selected?.Id != id) { ViewModel.SetStatus("The recipe selection changed. Select a visible recipe to edit."); return; }
        Open(RecipeEditDialogViewModel.ForEdit(selected, _ingredients));
    }
    private void Open(RecipeEditDialogViewModel model)
    {
        var draft = _interaction.ShowRecipeDialog(model);
        if (draft is null) { ViewModel.SetStatus("Recipe dialog cancelled. No recipe was changed."); return; }
        if (!ReferenceEquals(draft, model.Result)) throw new InvalidDataException("The recipe dialog did not return its validated draft.");
        ViewModel.RetainDraft(draft);
        ViewModel.SetStatus("Draft prepared — not saved. It remains below while this screen is open.");
    }
    private bool Run(Action action, string message)
    {
        if (_disposed) return false;
        try { LastError = null; action(); return true; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Data.Common.DbException)
        {
            LastError = exception; ViewModel.SetStatus(exception is InvalidDataException ? exception.Message : message, true); return false;
        }
        finally { RefreshCommands(); }
    }
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => RefreshCommands();
    private void RefreshCommands() { AddRecipeCommand.RaiseCanExecuteChanged(); EditRecipeCommand.RaiseCanExecuteChanged(); ReloadCommand.RaiseCanExecuteChanged(); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; ViewModel.PropertyChanged -= OnChanged; ViewModel.DisconnectActions(this); RefreshCommands();
    }
}
