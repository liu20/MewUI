using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Hover is the pointer resting in one place. Small movement must not restart the wait, or a hand
/// that is not quite still never hovers; leaving the element must end it.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class MouseHoverLogicTests
{
    private sealed class Probe : Control
    {
    }

    [TestMethod]
    public void DisposingTwiceIsHarmless()
    {
        var probe = new Probe();
        var logic = new MouseHoverLogic(probe);

        logic.Dispose();
        logic.Dispose();
    }

    [TestMethod]
    public void AttachingToNothingIsRejected()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new MouseHoverLogic(null!));
}
