using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Aprillz.MewUI.VisualStudio
{
    [Guid("ab8e29a0-07fa-4cb2-b6c6-d6a8bb0204d6")]
    public sealed class PreviewToolWindow : ToolWindowPane
    {
        public PreviewToolWindow() : base(null)
        {
            Caption = "MewUI Preview";
            // Closing the pane only hides it, so a running session stays warm and reopening
            // the window reattaches instantly; Stop ends it explicitly.
            Content = new PreviewToolWindowControl();
        }
    }
}
