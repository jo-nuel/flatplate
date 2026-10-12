using FlatPlate.Core.Contracts.Authentication;

namespace FlatPlate.App.Authentication;

/// <summary>
/// Keeps authentication data in memory and removes it when the token expires or the user signs out.
/// </summary>
public sealed class AuthenticationSession : IAuthenticationSession, IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _expiryCancellation;
    private string? _accessToken;
    private DateTimeOffset _expiresAtUtc;
    private AuthenticatedUser? _currentUser;

    public event EventHandler? SessionChanged;

    public AuthenticationSession(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
        _synchronizationContext = SynchronizationContext.Current;
    }

    public AuthenticatedUser? CurrentUser => IsAuthenticated ? _currentUser : null;

    public bool IsAuthenticated =>
        _currentUser is not null
        && !string.IsNullOrWhiteSpace(_accessToken)
        && _expiresAtUtc > _timeProvider.GetUtcNow();

    public void Start(AuthenticationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.AccessToken)
            || response.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            throw new ArgumentException(
                "The authentication response must contain a valid, unexpired token.",
                nameof(response));
        }

        _accessToken = response.AccessToken;
        _expiresAtUtc = response.ExpiresAtUtc;
        _currentUser = response.User;
        ScheduleExpiry(response.ExpiresAtUtc - _timeProvider.GetUtcNow());
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryGetAccessToken(out string? accessToken)
    {
        if (IsAuthenticated)
        {
            accessToken = _accessToken;
            return true;
        }

        if (_currentUser is not null || _accessToken is not null)
        {
            Clear();
        }

        accessToken = null;
        return false;
    }

    public void Clear()
    {
        var hadSession = _currentUser is not null || _accessToken is not null;
        _expiryCancellation?.Cancel();
        _expiryCancellation?.Dispose();
        _expiryCancellation = null;
        _accessToken = null;
        _expiresAtUtc = default;
        _currentUser = null;

        if (hadSession)
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _expiryCancellation?.Cancel();
        _expiryCancellation?.Dispose();
        _expiryCancellation = null;
    }

    private void ScheduleExpiry(TimeSpan delay)
    {
        _expiryCancellation?.Cancel();
        _expiryCancellation?.Dispose();
        _expiryCancellation = new CancellationTokenSource();
        _ = ExpireAsync(delay, _expiryCancellation.Token);
    }

    private async Task ExpireAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_synchronizationContext is null)
        {
            Clear();
            return;
        }

        _synchronizationContext.Post(
            _ =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Clear();
                }
            },
            null);
    }
}
