using Aprillz.MewUI.Input;

namespace Aprillz.MewUI.MewvalonEdit.Editing;

/// <summary>
/// A set of key bindings and event handlers for a text area. One handler is active at a time
/// (<see cref="TextArea.ActiveInputHandler"/>); stacked handlers work in addition to it and see keys
/// first, which is how a search panel or a completion list takes over the keyboard without
/// detaching what is already there.
/// </summary>
public interface ITextAreaInputHandler
{
    TextArea TextArea { get; }

    /// <summary>Called when the handler becomes active. Bindings take effect from here.</summary>
    void Attach();

    /// <summary>Called when the handler stops being active.</summary>
    void Detach();
}

/// <summary>
/// A handler that sees keys before the active handler and before the editor acts on them. Stacked
/// handlers are asked in reverse order of being pushed, and detach in that order too.
/// </summary>
public abstract class TextAreaStackedInputHandler(TextArea textArea) : ITextAreaInputHandler
{
    public TextArea TextArea { get; } = textArea ?? throw new ArgumentNullException(nameof(textArea));

    public virtual void Attach()
    {
    }

    public virtual void Detach()
    {
    }

    /// <summary>
    /// Called before the active handler and the editor see the key. Set
    /// <see cref="KeyEventArgs.Handled"/> to claim it.
    /// </summary>
    public virtual void OnPreviewKeyDown(KeyEventArgs e)
    {
    }

    /// <summary>Called before the active handler and the editor see the key release.</summary>
    public virtual void OnPreviewKeyUp(KeyEventArgs e)
    {
    }
}

/// <summary>
/// A gesture bound to an action, answered at the preview stage before the editing surface acts on
/// the key.
/// </summary>
/// <remarks>
/// The extension owns this type rather than using the core shortcut binding: these run ahead of
/// the surface, which is a stage the command system's post-bubble input map cannot express, and
/// the dependency would tie the editor to a core type that exists for window-wide shortcuts.
/// </remarks>
public sealed class TextAreaKeyBinding
{
    public TextAreaKeyBinding(KeyGesture gesture, Action execute, Func<bool>? canExecute = null)
    {
        Gesture = gesture;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        CanExecute = canExecute;
    }

    /// <summary>The gesture that triggers this binding.</summary>
    public KeyGesture Gesture { get; }

    /// <summary>The action to run when the gesture matches.</summary>
    public Action Execute { get; }

    /// <summary>Decides whether the binding runs. A binding that declines leaves the key unclaimed.</summary>
    public Func<bool>? CanExecute { get; set; }

    /// <summary>Runs the binding when the gesture matches and it can execute, claiming the key.</summary>
    public bool TryHandle(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Gesture.Matches(e) && (CanExecute?.Invoke() ?? true))
        {
            Execute();
            e.Handled = true;
            return true;
        }
        return false;
    }
}

/// <summary>
/// The ordinary input handler: a set of key bindings plus nested handlers that attach and detach
/// with it. The original keeps commands and gestures in two collections because WPF routes them
/// separately; a <see cref="TextAreaKeyBinding"/> already carries both, so there is one collection.
/// </summary>
public class TextAreaInputHandler : ITextAreaInputHandler
{
    private readonly List<TextAreaKeyBinding> _keyBindings = [];
    private readonly List<ITextAreaInputHandler> _nestedInputHandlers = [];

    public TextAreaInputHandler(TextArea textArea)
        => TextArea = textArea ?? throw new ArgumentNullException(nameof(textArea));

    public TextArea TextArea { get; }

    public bool IsAttached { get; private set; }

    /// <summary>Bindings this handler answers with while it is attached.</summary>
    public IReadOnlyList<TextAreaKeyBinding> KeyBindings => _keyBindings;

    /// <summary>Handlers that attach and detach together with this one.</summary>
    public IReadOnlyList<ITextAreaInputHandler> NestedInputHandlers => _nestedInputHandlers;

    public void AddBinding(TextAreaKeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _keyBindings.Add(binding);
    }

    /// <summary>Binds a gesture to an action, which is the shape almost every caller wants.</summary>
    public void AddBinding(KeyGesture gesture, Action execute)
        => AddBinding(new TextAreaKeyBinding(gesture, execute));

    public bool RemoveBinding(TextAreaKeyBinding binding) => _keyBindings.Remove(binding);

    public void AddNestedInputHandler(ITextAreaInputHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (handler.TextArea != TextArea)
        {
            throw new ArgumentException("The nested handler must belong to the same text area.", nameof(handler));
        }
        _nestedInputHandlers.Add(handler);
        if (IsAttached)
        {
            handler.Attach();
        }
    }

    public bool RemoveNestedInputHandler(ITextAreaInputHandler handler)
    {
        if (!_nestedInputHandlers.Remove(handler))
        {
            return false;
        }
        if (IsAttached)
        {
            handler.Detach();
        }
        return true;
    }

    public virtual void Attach()
    {
        if (IsAttached)
        {
            throw new InvalidOperationException("The input handler is already attached.");
        }
        IsAttached = true;
        foreach (var handler in _nestedInputHandlers)
        {
            handler.Attach();
        }
    }

    public virtual void Detach()
    {
        if (!IsAttached)
        {
            throw new InvalidOperationException("The input handler is not attached.");
        }
        IsAttached = false;
        foreach (var handler in _nestedInputHandlers)
        {
            handler.Detach();
        }
    }

    /// <summary>
    /// Runs the bindings of this handler and of its nested handlers against a key, deepest first so
    /// a nested handler can override the one that hosts it.
    /// </summary>
    internal bool TryHandleKey(KeyEventArgs e)
    {
        foreach (var handler in _nestedInputHandlers)
        {
            if (handler is TextAreaInputHandler nested && nested.TryHandleKey(e))
            {
                return true;
            }
        }
        foreach (var binding in _keyBindings)
        {
            if (binding.TryHandle(e))
            {
                return true;
            }
        }
        return false;
    }
}
