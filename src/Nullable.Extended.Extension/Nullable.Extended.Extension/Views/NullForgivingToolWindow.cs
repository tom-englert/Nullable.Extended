using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Nullable.Extended.Extension.Views
{
    [Guid("5e194576-2306-4098-ab46-1cdd4e8e8579")]
    public class NullForgivingToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullForgivingToolWindow"/> class.
        /// </summary>
        public NullForgivingToolWindow() : base(null)
        {
            Caption = "Null Forgiving Operators";
        }

        protected override void Initialize()
        {
            base.Initialize();

            Content = ((ExtensionPackage)Package).ExportProvider.GetToolWindowShell(nameof(NullForgivingToolWindow));
        }
    }
}
