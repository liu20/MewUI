using System.Text;
using System.Text.RegularExpressions;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>
/// Turns a parsed .xshd document into a usable definition: names are registered, references are
/// resolved, imports are pulled in, and keyword lists become regexes.
/// </summary>
internal sealed class XmlHighlightingDefinition : IHighlightingDefinition
{
    private readonly Dictionary<string, HighlightingRuleSet> _ruleSetDict = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HighlightingColor> _colorDict = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _propDict = new(StringComparer.Ordinal);

    public XmlHighlightingDefinition(
        XshdSyntaxDefinition xshd,
        IHighlightingDefinitionReferenceResolver? resolver)
    {
        Name = xshd.Name ?? string.Empty;

        // Named elements first, so a reference can be resolved before the element it points at has
        // been translated. That is what lets two rule sets refer to each other.
        var register = new RegisterNamedElementsVisitor(this);
        xshd.AcceptElements(register);

        foreach (var element in xshd.Elements)
        {
            if (element is XshdRuleSet ruleSet && ruleSet.Name is null)
            {
                if (MainRuleSet is not null)
                {
                    throw Error(element, "Duplicate main RuleSet. There must be only one nameless RuleSet!");
                }
                MainRuleSet = register.RuleSets[ruleSet];
            }
        }
        if (MainRuleSet is null)
        {
            throw new HighlightingDefinitionInvalidException("Could not find main RuleSet.");
        }

        xshd.AcceptElements(new TranslateElementVisitor(this, register.RuleSets, resolver));

        foreach (var property in xshd.Elements.OfType<XshdProperty>())
        {
            if (property.Name is string name)
            {
                _propDict[name] = property.Value ?? string.Empty;
            }
        }
    }

    public string Name { get; }

    public HighlightingRuleSet MainRuleSet { get; } = null!;

    public IEnumerable<HighlightingColor> NamedHighlightingColors => _colorDict.Values;

    public IDictionary<string, string> Properties => _propDict;

    public HighlightingRuleSet? GetNamedRuleSet(string name)
        => string.IsNullOrEmpty(name) ? MainRuleSet : _ruleSetDict.GetValueOrDefault(name);

    public HighlightingColor? GetNamedColor(string name) => _colorDict.GetValueOrDefault(name);

    private static Exception Error(XshdElement element, string message)
        => element.LineNumber > 0
            ? new HighlightingDefinitionInvalidException($"Error at line {element.LineNumber}:\n{message}")
            : new HighlightingDefinitionInvalidException(message);

    /// <summary>Creates the empty instance every named rule set and colour will be filled into.</summary>
    private sealed class RegisterNamedElementsVisitor(XmlHighlightingDefinition definition) : IXshdVisitor
    {
        public Dictionary<XshdRuleSet, HighlightingRuleSet> RuleSets { get; } = [];

        public object? VisitRuleSet(XshdRuleSet ruleSet)
        {
            var translated = new HighlightingRuleSet();
            RuleSets.Add(ruleSet, translated);
            if (ruleSet.Name is string name)
            {
                if (name.Length == 0)
                {
                    throw Error(ruleSet, "Name must not be the empty string");
                }
                if (!definition._ruleSetDict.TryAdd(name, translated))
                {
                    throw Error(ruleSet, $"Duplicate rule set name '{name}'.");
                }
            }
            ruleSet.AcceptElements(this);
            return null;
        }

        public object? VisitColor(XshdColor color)
        {
            if (color.Name is string name)
            {
                if (name.Length == 0)
                {
                    throw Error(color, "Name must not be the empty string");
                }
                if (!definition._colorDict.TryAdd(name, new HighlightingColor()))
                {
                    throw Error(color, $"Duplicate color name '{name}'.");
                }
            }
            return null;
        }

        public object? VisitKeywords(XshdKeywords keywords) => keywords.ColorReference.AcceptVisitor(this);

        public object? VisitSpan(XshdSpan span)
        {
            span.BeginColorReference.AcceptVisitor(this);
            span.SpanColorReference.AcceptVisitor(this);
            span.EndColorReference.AcceptVisitor(this);
            return span.RuleSetReference.AcceptVisitor(this);
        }

        public object? VisitImport(XshdImport import) => import.RuleSetReference.AcceptVisitor(this);

        public object? VisitRule(XshdRule rule) => rule.ColorReference.AcceptVisitor(this);
    }

    private sealed class TranslateElementVisitor : IXshdVisitor
    {
        private readonly XmlHighlightingDefinition _definition;
        private readonly Dictionary<XshdRuleSet, HighlightingRuleSet> _ruleSetDict;
        private readonly Dictionary<HighlightingRuleSet, XshdRuleSet> _reverseRuleSetDict = [];
        private readonly IHighlightingDefinitionReferenceResolver? _resolver;
        private readonly HashSet<XshdRuleSet> _processingStartedRuleSets = [];
        private readonly HashSet<XshdRuleSet> _processedRuleSets = [];
        private bool _ignoreCase;

        public TranslateElementVisitor(
            XmlHighlightingDefinition definition,
            Dictionary<XshdRuleSet, HighlightingRuleSet> ruleSetDict,
            IHighlightingDefinitionReferenceResolver? resolver)
        {
            _definition = definition;
            _ruleSetDict = ruleSetDict;
            _resolver = resolver;
            foreach (var pair in ruleSetDict)
            {
                _reverseRuleSetDict.Add(pair.Value, pair.Key);
            }
        }

        public object? VisitRuleSet(XshdRuleSet ruleSet)
        {
            var translated = _ruleSetDict[ruleSet];
            if (_processedRuleSets.Contains(ruleSet))
            {
                return translated;
            }
            if (!_processingStartedRuleSets.Add(ruleSet))
            {
                throw Error(ruleSet, "RuleSet cannot be processed because it contains cyclic <Import>");
            }

            bool outerIgnoreCase = _ignoreCase;
            if (ruleSet.IgnoreCase is bool ignoreCase)
            {
                _ignoreCase = ignoreCase;
            }
            translated.Name = ruleSet.Name;

            foreach (var element in ruleSet.Elements)
            {
                switch (element.AcceptVisitor(this))
                {
                    case HighlightingRuleSet imported:
                        foreach (var rule in imported.Rules) translated.Rules.Add(rule);
                        foreach (var span in imported.Spans) translated.Spans.Add(span);
                        break;
                    case HighlightingSpan span:
                        translated.Spans.Add(span);
                        break;
                    case HighlightingRule rule:
                        translated.Rules.Add(rule);
                        break;
                }
            }

            _ignoreCase = outerIgnoreCase;
            _processedRuleSets.Add(ruleSet);
            return translated;
        }

        public object? VisitColor(XshdColor color)
        {
            HighlightingColor translated;
            if (color.Name is string name)
            {
                translated = _definition._colorDict[name];
            }
            else if (color.Foreground is null && color.Background is null && color.Underline is null
                && color.Italic is null && color.FontWeight is null)
            {
                return null;
            }
            else
            {
                translated = new HighlightingColor();
            }

            translated.Name = color.Name;
            translated.Foreground = color.Foreground;
            translated.Background = color.Background;
            translated.Underline = color.Underline;
            translated.Strikethrough = color.Strikethrough;
            translated.Italic = color.Italic;
            translated.FontWeight = color.FontWeight;
            translated.FontFamily = color.FontFamily;
            translated.FontSize = color.FontSize;
            return translated;
        }

        public object? VisitKeywords(XshdKeywords keywords)
        {
            if (keywords.Words.Count == 0)
            {
                throw Error(keywords, "Keyword group must not be empty.");
            }
            foreach (string keyword in keywords.Words)
            {
                if (string.IsNullOrEmpty(keyword))
                {
                    throw Error(keywords, "Cannot use empty string as keyword");
                }
            }

            var pattern = new StringBuilder();
            // The atomic group is what makes a long keyword list cheap, but it captures the first
            // alternative that matches, so the words go in longest first: "\b(?>in|int)\b" would
            // never match "int". A word boundary only works where the keyword starts and ends with
            // a letter or digit, which ".maxstack" does not.
            if (keywords.Words.All(IsSimpleWord))
            {
                pattern.Append(@"\b(?>");
                int index = 0;
                foreach (string keyword in keywords.Words.OrderByDescending(word => word.Length))
                {
                    if (index++ > 0) pattern.Append('|');
                    pattern.Append(Regex.Escape(keyword));
                }
                pattern.Append(@")\b");
            }
            else
            {
                pattern.Append("(?>");
                int index = 0;
                foreach (string keyword in keywords.Words.OrderByDescending(word => word.Length))
                {
                    if (index++ > 0) pattern.Append('|');
                    if (char.IsLetterOrDigit(keyword[0])) pattern.Append(@"\b");
                    pattern.Append(Regex.Escape(keyword));
                    if (char.IsLetterOrDigit(keyword[^1])) pattern.Append(@"\b");
                }
                pattern.Append(')');
            }

            return new HighlightingRule
            {
                Color = GetColor(keywords, keywords.ColorReference),
                Regex = CreateRegex(keywords, pattern.ToString(), XshdRegexType.Default)
            };
        }

        public object? VisitSpan(XshdSpan span)
        {
            string? endRegex = span.EndRegex;
            if (string.IsNullOrEmpty(span.BeginRegex) && string.IsNullOrEmpty(span.EndRegex))
            {
                throw Error(span, "Span has no start/end regex.");
            }
            if (!span.Multiline)
            {
                if (endRegex is null)
                {
                    endRegex = "$";
                }
                else if (span.EndRegexType == XshdRegexType.IgnorePatternWhitespace)
                {
                    endRegex = "($|" + endRegex + "\n)";
                }
                else
                {
                    endRegex = "($|" + endRegex + ")";
                }
            }
            return new HighlightingSpan
            {
                StartExpression = CreateRegex(span, span.BeginRegex, span.BeginRegexType),
                EndExpression = CreateRegex(span, endRegex, span.EndRegexType),
                RuleSet = GetRuleSet(span, span.RuleSetReference),
                StartColor = GetColor(span, span.BeginColorReference),
                SpanColor = GetColor(span, span.SpanColorReference),
                EndColor = GetColor(span, span.EndColorReference),
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
        }

        public object? VisitImport(XshdImport import)
        {
            var ruleSet = GetRuleSet(import, import.RuleSetReference);
            if (ruleSet is not null && _reverseRuleSetDict.TryGetValue(ruleSet, out var source))
            {
                // Translate the imported set before taking its members, or it would be copied empty.
                VisitRuleSet(source);
            }
            return ruleSet;
        }

        public object? VisitRule(XshdRule rule)
            => new HighlightingRule
            {
                Color = GetColor(rule, rule.ColorReference),
                Regex = CreateRegex(rule, rule.Regex, rule.RegexType)
            };

        private static bool IsSimpleWord(string word)
            => char.IsLetterOrDigit(word[0]) && char.IsLetterOrDigit(word[^1]);

        private Regex CreateRegex(XshdElement position, string? regex, XshdRegexType regexType)
        {
            if (regex is null)
            {
                throw Error(position, "Regex missing");
            }
            var options = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;
            if (regexType == XshdRegexType.IgnorePatternWhitespace)
            {
                options |= RegexOptions.IgnorePatternWhitespace;
            }
            if (_ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }
            try
            {
                return new Regex(regex, options);
            }
            catch (ArgumentException error)
            {
                throw Error(position, error.Message);
            }
        }

        private HighlightingColor? GetColor(XshdElement position, XshdReference<XshdColor> reference)
        {
            if (reference.InlineElement is XshdColor inline)
            {
                return (HighlightingColor?)inline.AcceptVisitor(this);
            }
            if (reference.ReferencedElement is string name)
            {
                var color = GetDefinition(position, reference.ReferencedDefinition).GetNamedColor(name);
                return color ?? throw Error(position, $"Could not find color named '{name}'.");
            }
            return null;
        }

        private HighlightingRuleSet? GetRuleSet(XshdElement position, XshdReference<XshdRuleSet> reference)
        {
            if (reference.InlineElement is XshdRuleSet inline)
            {
                return (HighlightingRuleSet?)inline.AcceptVisitor(this);
            }
            if (reference.ReferencedElement is string name)
            {
                var ruleSet = GetDefinition(position, reference.ReferencedDefinition).GetNamedRuleSet(name);
                return ruleSet ?? throw Error(position, $"Could not find rule set named '{name}'.");
            }
            return null;
        }

        private IHighlightingDefinition GetDefinition(XshdElement position, string? definitionName)
        {
            if (definitionName is null)
            {
                return _definition;
            }
            if (_resolver is null)
            {
                throw Error(position,
                    "Resolving references to other syntax definitions is not possible because the IHighlightingDefinitionReferenceResolver is null.");
            }
            return _resolver.GetDefinition(definitionName)
                ?? throw Error(position, $"Could not find definition with name '{definitionName}'.");
        }
    }
}
