using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Input;
using ReactiveUI;
using System.Reactive.Linq;
using System;

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
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public PinLockScreen() : base()
		{
			InitializeComponent();

			var inputField = this.FindControl<NoparaPasswordBox>("InputField");
			
			this.WhenAnyValue(x => x.IsLocked)
				.Where(x => x)
				.Subscribe(x => inputField.Focus());
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
