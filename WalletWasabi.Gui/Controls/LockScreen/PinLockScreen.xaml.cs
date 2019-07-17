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
using System.Reactive.Linq;
using System.Security.Cryptography;
using WalletWasabi.Helpers;
using System.Reactive;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    public class PinLockScreen : UserControl
    {
		public static readonly DirectProperty<PinLockScreen, bool> IsLockedProperty =
			AvaloniaProperty.RegisterDirect<PinLockScreen, bool>(nameof(IsLocked),
															  o => o.IsLocked,
															  (o, v) => o.IsLocked = v);
        private bool _isLocked;

        public bool IsLocked
		{
			get => _isLocked;
			set => this.SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

        public PinLockScreen() : base()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}