using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using Nullable.Extended.Extension.Views;

namespace Nullable.Extended.Extension
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("5e194576-2306-4098-ab46-1cdd4e8e8579")]
    public class ToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class.
        /// </summary>
        public ToolWindow() : base(null)
        {
            Caption = "Nullable.Extended";
        }

        protected override void Initialize()
        {
            base.Initialize();

            Content = ((ExtensionPackage)Package).ExportProvider.GetExportedValue<ToolWindowShell>();
        }
    }
}
