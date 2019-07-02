using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using Avalonia;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    internal class SimpleLock : LockScreenImpl
    { 
        public SimpleLock()
        {
            InitializeComponent(); 
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
