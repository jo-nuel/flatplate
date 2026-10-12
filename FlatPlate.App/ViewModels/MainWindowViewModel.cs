using System.Windows.Input;
using FlatPlate.App.Authentication;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Switches the application between the account gate and signed-in workspace.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IAuthenticationSession _session;

    public MainWindowViewModel(IAuthenticationSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.SessionChanged += HandleSessionChanged;
        SignOutCommand = new RelayCommand(_session.Clear);
    }

    public bool IsAuthenticated => _session.IsAuthenticated;

    public bool IsSignedOut => !IsAuthenticated;

    public string DisplayName => _session.CurrentUser?.DisplayName ?? string.Empty;

    public string WelcomeMessage => string.IsNullOrWhiteSpace(DisplayName)
        ? "Welcome to FlatPlate"
        : $"Welcome back, {DisplayName}";

    public ICommand SignOutCommand { get; }

    public void Dispose()
    {
        _session.SessionChanged -= HandleSessionChanged;
    }

    private void HandleSessionChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(WelcomeMessage));
    }
}
