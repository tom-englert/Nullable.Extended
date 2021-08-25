using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using Nullable.Extended.Extension.Extension;

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
            BitmapResourceID = 301;
            BitmapIndex = 0;
        }

        protected override void Initialize()
        {
            base.Initialize();

            Content = ((ExtensionPackage)Package).ExportProvider.GetToolWindowShell(nameof(NullForgivingToolWindow));
        }

        protected override bool PreProcessMessage(ref System.Windows.Forms.Message m)
        {
            // Do not pass CTRL+'F' to visual studio, we do our own search
            if (m.Msg != 0x0100 
                || m.WParam != (IntPtr)0x46 && (m.WParam != (IntPtr)0x66) 
                || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return base.PreProcessMessage(ref m);
            }

            var keyboardDevice = Keyboard.PrimaryDevice;

            var e = new KeyEventArgs(keyboardDevice, keyboardDevice.ActiveSource, 0, Key.F)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            _ = InputManager.Current.ProcessInput(e);

            return true;
        }
    }
}
