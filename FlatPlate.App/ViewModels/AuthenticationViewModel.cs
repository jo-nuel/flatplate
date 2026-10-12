using System.Windows.Input;
using FlatPlate.App.Authentication;
using FlatPlate.Core.Contracts.Authentication;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Coordinates the account forms without retaining passwords in view-model state.
/// </summary>
public sealed class AuthenticationViewModel : ViewModelBase
{
    private readonly IAuthenticationApiClient _apiClient;
    private readonly IAuthenticationSession _session;
    private AuthenticationPage _currentPage = AuthenticationPage.SignIn;
    private string _displayName = string.Empty;
    private string _email = string.Empty;
    private string _verificationCode = string.Empty;
    private string _resetCode = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public AuthenticationViewModel(
        IAuthenticationApiClient apiClient,
        IAuthenticationSession session)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        ShowSignInCommand = new RelayCommand(() => ShowPage(AuthenticationPage.SignIn));
        ShowRegisterCommand = new RelayCommand(() => ShowPage(AuthenticationPage.Register));
        ShowVerifyEmailCommand = new RelayCommand(
            () => ShowPage(AuthenticationPage.VerifyEmail));
        ShowResetPasswordCommand = new RelayCommand(
            () => ShowPage(AuthenticationPage.ResetPassword));
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string VerificationCode
    {
        get => _verificationCode;
        set => SetProperty(ref _verificationCode, value);
    }

    public string ResetCode
    {
        get => _resetCode;
        set => SetProperty(ref _resetCode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public bool CanSubmit => !IsBusy;

    public bool IsSignInVisible => _currentPage == AuthenticationPage.SignIn;

    public bool IsRegisterVisible => _currentPage == AuthenticationPage.Register;

    public bool IsVerifyEmailVisible => _currentPage == AuthenticationPage.VerifyEmail;

    public bool IsResetPasswordVisible => _currentPage == AuthenticationPage.ResetPassword;

    public ICommand ShowSignInCommand { get; }

    public ICommand ShowRegisterCommand { get; }

    public ICommand ShowVerifyEmailCommand { get; }

    public ICommand ShowResetPasswordCommand { get; }

    public async Task<bool> SignInAsync(
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!BeginRequest())
        {
            return false;
        }

        try
        {
            var result = await _apiClient.SignInAsync(
                new SignInRequest(Email, password),
                cancellationToken);
            if (!result.IsSuccess)
            {
                ShowFailure(result);
                return false;
            }

            _session.Start(result.Value!);
            StatusMessage = string.Empty;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RegisterAsync(
        string password,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (!BeginRequest())
        {
            return false;
        }

        try
        {
            var result = await _apiClient.RegisterAsync(
                new RegisterRequest(DisplayName, Email, password, confirmPassword),
                cancellationToken);
            if (!result.IsSuccess)
            {
                ShowFailure(result);
                return false;
            }

            ShowPage(AuthenticationPage.VerifyEmail);
            StatusMessage = result.Value!.Message;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> VerifyEmailAsync(
        CancellationToken cancellationToken = default)
    {
        if (!BeginRequest())
        {
            return false;
        }

        try
        {
            var result = await _apiClient.VerifyEmailAsync(
                new VerifyEmailRequest(Email, VerificationCode),
                cancellationToken);
            if (!result.IsSuccess)
            {
                ShowFailure(result);
                return false;
            }

            ShowPage(AuthenticationPage.SignIn);
            StatusMessage = result.Value!.Message;
            VerificationCode = string.Empty;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RequestPasswordResetAsync(
        CancellationToken cancellationToken = default)
    {
        if (!BeginRequest())
        {
            return false;
        }

        try
        {
            var result = await _apiClient.RequestPasswordResetAsync(
                new RequestPasswordResetRequest(Email),
                cancellationToken);
            if (!result.IsSuccess)
            {
                ShowFailure(result);
                return false;
            }

            StatusMessage = result.Value!.Message;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ResetPasswordAsync(
        string newPassword,
        string confirmNewPassword,
        CancellationToken cancellationToken = default)
    {
        if (!BeginRequest())
        {
            return false;
        }

        try
        {
            var result = await _apiClient.ResetPasswordAsync(
                new ResetPasswordRequest(
                    Email,
                    ResetCode,
                    newPassword,
                    confirmNewPassword),
                cancellationToken);
            if (!result.IsSuccess)
            {
                ShowFailure(result);
                return false;
            }

            ShowPage(AuthenticationPage.SignIn);
            StatusMessage = result.Value!.Message;
            ResetCode = string.Empty;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool BeginRequest()
    {
        if (IsBusy)
        {
            return false;
        }

        StatusMessage = string.Empty;
        IsBusy = true;
        return true;
    }

    private void ShowPage(AuthenticationPage page)
    {
        _currentPage = page;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(IsSignInVisible));
        OnPropertyChanged(nameof(IsRegisterVisible));
        OnPropertyChanged(nameof(IsVerifyEmailVisible));
        OnPropertyChanged(nameof(IsResetPasswordVisible));
    }

    private void ShowFailure<T>(AuthenticationClientResult<T> result)
    {
        StatusMessage = result.ValidationErrors is { Count: > 0 }
            ? string.Join(
                Environment.NewLine,
                result.ValidationErrors.Select(error => error.Message))
            : result.Message ?? "The account request could not be completed.";
    }
}
