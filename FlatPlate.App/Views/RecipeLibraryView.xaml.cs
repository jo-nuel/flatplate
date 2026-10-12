using System.Windows.Controls;
using FlatPlate.App.Services;

namespace FlatPlate.App.Views;

/// <summary>
/// Shows the household recipe collection with search, cuisine filter and recipe details.
/// </summary>
public partial class RecipeLibraryView : UserControl
{
    /// <summary>
    /// Creates the Recipe Library view.
    /// </summary>
    public RecipeLibraryView()
    {
        InitializeComponent();
    }

    /// <summary>Connects host-supplied reads and interactions without creating a database.</summary>
    public void Connect(RecipeLibraryCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        DataContext = coordinator.ViewModel;
        coordinator.Load();
    }
}
