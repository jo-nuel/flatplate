using System.Windows.Controls;
using FlatPlate.App.Services;

namespace FlatPlate.App.Views;

/// <summary>
/// Shows supermarket pack prices for each ingredient with store filtering and CSV entry points.
/// </summary>
public partial class PriceBookView : UserControl
{
    /// <summary>
    /// Creates the Price Book view.
    /// </summary>
    public PriceBookView()
    {
        InitializeComponent();
    }

    /// <summary>Explicit host composition hook. No context or path is created by this view.</summary>
    public void Connect(PriceBookCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        DataContext = coordinator.ViewModel;
        coordinator.Load();
    }
}
