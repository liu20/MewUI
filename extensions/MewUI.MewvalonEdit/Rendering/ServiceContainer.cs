namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Service lookup keyed by type. Ported code registers what it builds here and finds it again by
/// service type, which is how a highlighter reaches code that only holds a view.
/// </summary>
public sealed class ServiceContainer
{
    private readonly Dictionary<Type, object> _services = [];

    /// <summary>Registers the instance under <typeparamref name="TService"/>.</summary>
    public void AddService<TService>(TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _services[typeof(TService)] = instance;
    }

    /// <summary>Removes the registration, if any.</summary>
    public bool RemoveService<TService>() where TService : class => _services.Remove(typeof(TService));

    /// <summary>The registered instance, or null.</summary>
    public TService? GetService<TService>() where TService : class
        => _services.GetValueOrDefault(typeof(TService)) as TService;
}
