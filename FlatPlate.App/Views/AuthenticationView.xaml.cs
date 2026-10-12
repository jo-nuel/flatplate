using System.Windows;
using System.Windows.Controls;
using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Views;

/// <summary>
/// Hosts account forms while keeping password values inside password controls.
/// </summary>
public partial class AuthenticationView : UserControl
{
    public AuthenticationView()
    {
        InitializeComponent();
    }

    private AuthenticationViewModel? ViewModel => DataContext as AuthenticationViewModel;

    private async void SignIn_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not null
            && await ViewModel.SignInAsync(SignInPassword.Password))
        {
            SignInPassword.Clear();
        }
    }

    private async void Register_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not null
            && await ViewModel.RegisterAsync(
                RegisterPassword.Password,
                RegisterConfirmPassword.Password))
        {
            RegisterPassword.Clear();
            RegisterConfirmPassword.Clear();
        }
    }

    private async void VerifyEmail_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not null)
        {
            await ViewModel.VerifyEmailAsync();
        }
    }

    private async void ResendVerification_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RequestEmailVerificationAsync();
        }
    }

    private async void RequestReset_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RequestPasswordResetAsync();
        }
    }

    private async void ResetPassword_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not null
            && await ViewModel.ResetPasswordAsync(
                ResetPassword.Password,
                ResetConfirmPassword.Password))
        {
            ResetPassword.Clear();
            ResetConfirmPassword.Clear();
        }
    }
}
