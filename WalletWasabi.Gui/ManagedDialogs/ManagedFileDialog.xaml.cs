using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.ManagedDialogs
{
    class ManagedFileDialog : Window
    {
        private ManagedFileChooserViewModel _model;
        public ManagedFileDialog()
        {
            AvaloniaXamlLoader.Load(this);
            #if DEBUG
                this.AttachDevTools();
            #endif
        }
    }
}
