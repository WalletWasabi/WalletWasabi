using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using Avalonia;
using Avalonia.Interactivity;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    internal class SimpleLock : LockScreenImpl
    {
        public Grid Shade { get; }

        public SimpleLock()
        {
            InitializeComponent();
            var unlockButton = this.FindControl<Button>("UnlockButton");

            this.Shade = this.FindControl<Grid>("Shade");

            unlockButton.Click += unlockButton_Clicked;

            this.WhenAnyValue(x => x.IsLocked)
                .Subscribe(IsLockedChanged);
        }

        private void IsLockedChanged(bool obj)
        {
            if (obj)
            {
                Shade.Classes.Add("Locked");
                Shade.Classes.Remove("Unlocked");
            }
            else
            {
                Shade.Classes.Add("Unlocked");
                Shade.Classes.Remove("Locked");
            }
        }

        private void unlockButton_Clicked(object sender, RoutedEventArgs e)
        {
            this.IsLocked = false;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
