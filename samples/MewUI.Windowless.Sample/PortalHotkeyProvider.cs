using System.Diagnostics;

using Aprillz.MewUI.Windowless.Sample.DBus;

using Tmds.DBus.Protocol;

namespace Aprillz.MewUI.Windowless.Sample;

internal sealed class PortalHotkeyProvider(Func<IHotkeyProvider> fallbackFactory) : IHotkeyProvider
{
    private const string DESTINATION = "org.freedesktop.portal.Desktop";
    private const string OBJECT_PATH = "/org/freedesktop/portal/desktop";
    private const string SHORTCUT_ID = "toggle-palette";

    private readonly CancellationTokenSource _stop = new();
    private IHotkeyProvider? _fallback;
    private DBusConnection? _connection;
    private Session? _session;
    private IDisposable? _activatedSubscription;
    private Task? _initialization;
    private Action? _activated;

    public string Name => _fallback?.Name ?? "xdg-desktop-portal GlobalShortcuts";

    public void Start(Action activated)
    {
        ArgumentNullException.ThrowIfNull(activated);
        _activated = activated;
        _initialization = InitializeOrFallbackAsync(_stop.Token);
    }

    private async Task InitializeOrFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InitializePortalAsync(cancellationToken);
            Console.Error.WriteLine("[windowless] GlobalShortcuts portal session active");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[windowless] GlobalShortcuts portal unavailable ({ex.Message}); falling back to X11");
            if (!cancellationToken.IsCancellationRequested)
            {
                _fallback = fallbackFactory();
                _fallback.Start(_activated!);
            }
        }
    }

    private async Task InitializePortalAsync(CancellationToken cancellationToken)
    {
        var address = DBusAddress.Session
            ?? throw new InvalidOperationException("No DBus session bus address is available.");
        var connection = new DBusConnection(address);
        await connection.ConnectAsync();
        cancellationToken.ThrowIfCancellationRequested();
        _connection = connection;

        var portal = new GlobalShortcuts(connection, DESTINATION, OBJECT_PATH);
        uint version = await portal.GetVersionAsync();
        if (version < 1)
        {
            throw new NotSupportedException($"GlobalShortcuts portal version {version} is not supported.");
        }

        var createOptions = new Dictionary<string, VariantValue>();
        var createRequest = CreateRequest(connection, createOptions, includeSessionToken: true);
        using var createResponse = await PortalResponseAwaiter.CreateAsync(
            connection, createRequest.ExpectedPath, cancellationToken);
        ObjectPath actualCreatePath = await portal.CreateSessionAsync(createOptions);
        EnsureExpectedPath(createRequest.ExpectedPath, actualCreatePath);
        var createResults = await createResponse.Response;
        if (!createResults.TryGetValue("session_handle", out var sessionValue))
        {
            throw new InvalidOperationException("GlobalShortcuts CreateSession returned no session handle.");
        }

        // The portal specification describes this value as an object path, but its
        // D-Bus result type is string for backwards compatibility.
        ObjectPath sessionPath = new(sessionValue.GetString());
        _session = new Session(connection, DESTINATION, sessionPath);
        _activatedSubscription = await portal.WatchActivatedAsync(notification =>
        {
            if (!notification.IsCompletion
                && notification.Value.SessionHandle == sessionPath
                && string.Equals(notification.Value.ShortcutId, SHORTCUT_ID, StringComparison.Ordinal))
            {
                _activated?.Invoke();
            }
        }, ObserverFlags.EmitAll);

        var shortcutOptions = new Dictionary<string, VariantValue>
        {
            ["description"] = VariantValue.String("Toggle the MewUI windowless palette"),
            ["preferred_trigger"] = VariantValue.String("CTRL+ALT+space"),
        };
        (string, Dictionary<string, VariantValue>)[] shortcuts = [(SHORTCUT_ID, shortcutOptions)];
        var bindOptions = new Dictionary<string, VariantValue>();
        var bindRequest = CreateRequest(connection, bindOptions, includeSessionToken: false);
        using var bindResponse = await PortalResponseAwaiter.CreateAsync(
            connection, bindRequest.ExpectedPath, cancellationToken);
        ObjectPath actualBindPath = await portal.BindShortcutsAsync(sessionPath, shortcuts, string.Empty, bindOptions);
        EnsureExpectedPath(bindRequest.ExpectedPath, actualBindPath);
        _ = await bindResponse.Response;
    }

    private sealed class PortalResponseAwaiter : IDisposable
    {
        private readonly TaskCompletionSource<Dictionary<string, VariantValue>> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private IDisposable? _subscription;

        private PortalResponseAwaiter(CancellationToken cancellationToken)
        {
            _cancellationRegistration = cancellationToken.Register(
                () => _completion.TrySetCanceled(cancellationToken));
        }

        public Task<Dictionary<string, VariantValue>> Response => _completion.Task;

        public static async Task<PortalResponseAwaiter> CreateAsync(
            DBusConnection connection,
            ObjectPath requestPath,
            CancellationToken cancellationToken)
        {
            var awaiter = new PortalResponseAwaiter(cancellationToken);
            var request = new Request(connection, DESTINATION, requestPath);
            awaiter._subscription = await request.WatchResponseAsync(notification =>
            {
                if (notification.IsCompletion)
                {
                    awaiter._completion.TrySetException(notification.Exception!);
                }
                else if (notification.Value.Response == 0)
                {
                    awaiter._completion.TrySetResult(notification.Value.Results);
                }
                else
                {
                    awaiter._completion.TrySetException(new InvalidOperationException(
                        $"Portal request ended with response {notification.Value.Response}."));
                }
            }, ObserverFlags.EmitAll);
            return awaiter;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
            _cancellationRegistration.Dispose();
        }
    }

    private static (ObjectPath ExpectedPath, string Token) CreateRequest(
        DBusConnection connection,
        Dictionary<string, VariantValue> options,
        bool includeSessionToken)
    {
        string sender = (connection.UniqueName ?? string.Empty).TrimStart(':').Replace('.', '_');
        string token = "mewui_" + Stopwatch.GetTimestamp();
        ObjectPath expectedPath = $"/org/freedesktop/portal/desktop/request/{sender}/{token}";
        options["handle_token"] = VariantValue.String(token);
        if (includeSessionToken)
        {
            options["session_handle_token"] = VariantValue.String("mewui_session_" + Stopwatch.GetTimestamp());
        }
        return (expectedPath, token);
    }

    private static void EnsureExpectedPath(ObjectPath expected, ObjectPath actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"Portal returned request path '{actual}', expected '{expected}'.");
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _activatedSubscription?.Dispose();
        _activatedSubscription = null;
        if (_session != null)
        {
            try { _session.CloseAsync().GetAwaiter().GetResult(); } catch { }
            _session = null;
        }
        _fallback?.Dispose();
        _fallback = null;
        if (_connection != null)
        {
            try { _connection.Dispose(); } catch { }
            _connection = null;
        }
        _activated = null;
        _stop.Dispose();
        _ = _initialization;
    }
}
