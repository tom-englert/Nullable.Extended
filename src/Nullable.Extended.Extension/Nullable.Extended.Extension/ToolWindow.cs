using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using Nullable.Extended.Extension.Views;

namespace Nullable.Extended.Extension
{
    [Guid("5e194576-2306-4098-ab46-1cdd4e8e8579")]
    public class ToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow"/> class.
        /// </summary>
        public ToolWindow() : base(null)
        {
            Caption = "Null forgiving analyzer";
        }

        protected override void Initialize()
        {
            base.Initialize();

            Content = ((ExtensionPackage)Package).ExportProvider.GetExportedValue<ToolWindowShell>();
        }
    }
}
