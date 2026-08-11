using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.MewvalonEdit.Folding;
using Aprillz.MewUI.MewvalonEdit.Rendering;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// The four-step lookup ported code relies on: editor to text area to view services to document
/// services. Ported code holds whichever of the three it was handed and still finds the rest.
/// </summary>
[TestClass]
public sealed class ServiceLocatorTests
{
    [TestMethod]
    public void EachComponentIsRegisteredOnTheView()
    {
        var editor = new TextEditor();

        Assert.AreSame(editor, editor.GetService<TextEditor>());
        Assert.AreSame(editor.TextArea, editor.TextArea.GetService<TextArea>());
        Assert.AreSame(editor.TextArea.TextView, editor.TextArea.TextView.GetService<TextView>());
    }

    [TestMethod]
    public void AnUnregisteredServiceIsNull()
    {
        var editor = new TextEditor();

        Assert.IsNull(editor.GetService<FoldingManager>());
    }

    /// <summary>
    /// A service put on the document is reached through a view of it, which is why the view's own
    /// container is not the whole lookup.
    /// </summary>
    [TestMethod]
    public void DocumentServicesAreReachedThroughTheView()
    {
        var editor = new TextEditor();
        var probe = new StringWriter();
        editor.Document.Services.AddService(probe);

        Assert.IsNull(editor.TextArea.TextView.Services.GetService<StringWriter>());
        Assert.AreSame(probe, editor.TextArea.TextView.GetService<StringWriter>());
        Assert.AreSame(probe, editor.GetService<StringWriter>());
    }

    [TestMethod]
    public void ADocumentCarriesItself()
    {
        var document = new TextDocument("text");

        Assert.AreSame(document, document.Services.GetService<TextDocument>());
    }

    /// <summary>A folding manager registers on install and is gone after uninstall.</summary>
    [TestMethod]
    public void TheFoldingManagerIsRegisteredWhileInstalled()
    {
        var editor = new TextEditor();

        var manager = FoldingManager.Install(editor);
        Assert.AreSame(manager, editor.TextArea.GetService<FoldingManager>());

        FoldingManager.Uninstall(manager);
        Assert.IsNull(editor.TextArea.GetService<FoldingManager>());
    }
}
