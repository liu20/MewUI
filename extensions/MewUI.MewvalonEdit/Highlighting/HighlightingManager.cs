using System.Xml;
using Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>
/// A list of syntax definitions, and the resolver a definition's cross-references go through.
/// </summary>
/// <remarks>All members, instance members included, are thread-safe.</remarks>
public class HighlightingManager : IHighlightingDefinitionReferenceResolver
{
    private readonly object _lock = new();
    private readonly Dictionary<string, IHighlightingDefinition> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IHighlightingDefinition> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IHighlightingDefinition> _all = [];

    /// <summary>The shared manager, carrying the definitions this assembly ships.</summary>
    public static HighlightingManager Instance => DefaultHighlightingManager.Default;

    /// <summary>A snapshot of every registered definition.</summary>
    public IReadOnlyList<IHighlightingDefinition> HighlightingDefinitions
    {
        get
        {
            lock (_lock)
            {
                return _all.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public IHighlightingDefinition? GetDefinition(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        lock (_lock)
        {
            return _byName.GetValueOrDefault(name);
        }
    }

    /// <summary>The definition registered for <paramref name="extension"/>, dot included.</summary>
    public IHighlightingDefinition? GetDefinitionByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }
        lock (_lock)
        {
            return _byExtension.GetValueOrDefault(extension);
        }
    }

    public void RegisterHighlighting(
        string? name,
        IEnumerable<string>? extensions,
        IHighlightingDefinition highlighting)
    {
        ArgumentNullException.ThrowIfNull(highlighting);
        lock (_lock)
        {
            if (name is not null)
            {
                if (_byName.TryGetValue(name, out var existing))
                {
                    _all.Remove(existing);
                }
                _byName[name] = highlighting;
            }
            if (extensions is not null)
            {
                foreach (string extension in extensions)
                {
                    _byExtension[extension] = highlighting;
                }
            }
            _all.Add(highlighting);
        }
    }

    /// <summary>
    /// Registers a definition parsed on first read, so it may reference definitions registered
    /// after it.
    /// </summary>
    public void RegisterHighlighting(
        string? name,
        IEnumerable<string>? extensions,
        Func<IHighlightingDefinition> lazyLoadedHighlighting)
    {
        ArgumentNullException.ThrowIfNull(lazyLoadedHighlighting);
        RegisterHighlighting(name, extensions, new DelayLoadedHighlightingDefinition(name, lazyLoadedHighlighting));
    }

    /// <summary>Registers one of the .xshd files shipped in this assembly.</summary>
    internal void RegisterBuiltIn(string name, string[]? extensions, string resourceName)
        => RegisterHighlighting(name, extensions, () =>
        {
            XshdSyntaxDefinition xshd;
            using (var stream = HighlightingResources.OpenStream(resourceName))
            using (var reader = XmlReader.Create(stream))
            {
                // The shipped definitions are known to match the schema, so validating them on
                // every start would only cost time.
                xshd = HighlightingLoader.LoadXshd(reader, skipValidation: true);
            }
            return HighlightingLoader.Load(xshd, this);
        });

    private sealed class DefaultHighlightingManager : HighlightingManager
    {
        public static readonly DefaultHighlightingManager Default = new();

        private DefaultHighlightingManager() => HighlightingResources.RegisterBuiltInHighlightings(this);
    }

    /// <summary>A definition that parses itself the first time one of its members is read.</summary>
    private sealed class DelayLoadedHighlightingDefinition(string? name, Func<IHighlightingDefinition> load)
        : IHighlightingDefinition
    {
        private readonly object _lock = new();
        private Func<IHighlightingDefinition>? _load = load;
        private IHighlightingDefinition? _definition;
        private Exception? _storedException;
        private bool _isLoading;

        public string Name => name ?? GetDefinition().Name;

        public HighlightingRuleSet MainRuleSet => GetDefinition().MainRuleSet;

        public IEnumerable<HighlightingColor> NamedHighlightingColors => GetDefinition().NamedHighlightingColors;

        public IDictionary<string, string> Properties => GetDefinition().Properties;

        public HighlightingRuleSet? GetNamedRuleSet(string ruleSetName) => GetDefinition().GetNamedRuleSet(ruleSetName);

        public HighlightingColor? GetNamedColor(string colorName) => GetDefinition().GetNamedColor(colorName);

        public override string ToString() => Name;

        private IHighlightingDefinition GetDefinition()
        {
            Func<IHighlightingDefinition> load;
            lock (_lock)
            {
                if (_definition is not null)
                {
                    return _definition;
                }
                if (_storedException is not null)
                {
                    throw new HighlightingDefinitionInvalidException(
                        "Error delay-loading highlighting definition", _storedException);
                }
                if (_isLoading)
                {
                    throw new InvalidOperationException(
                        "Tried to create delay-loaded highlighting definition recursively. Make sure there are no cyclic references between the highlighting definitions.");
                }
                _isLoading = true;
                load = _load!;
            }

            IHighlightingDefinition? loaded = null;
            Exception? failure = null;
            try
            {
                loaded = load();
            }
            catch (Exception error)
            {
                failure = error;
            }

            lock (_lock)
            {
                _isLoading = false;
                _load = null;
                _definition ??= loaded;
                _storedException ??= failure;
                if (_storedException is not null)
                {
                    throw new HighlightingDefinitionInvalidException(
                        "Error delay-loading highlighting definition", _storedException);
                }
                return _definition!;
            }
        }
    }
}
