using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Input;
using ReactiveUI;
using System.Reactive.Linq;
using System;
using Avalonia.LogicalTree;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public class PinLockScreen : UserControl
	{
		public static readonly DirectProperty<PinLockScreen, bool> IsLockedProperty =
			AvaloniaProperty.RegisterDirect<PinLockScreen, bool>(nameof(IsLocked), o => o.IsLocked, (o, v) => o.IsLocked = v);

		private bool _isLocked;

		public bool IsLocked
		{
			get => _isLocked;
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public PinLockScreen() : base()
		{
			InitializeComponent();

			var inputField = this.FindControl<TogglePasswordBox>("InputField");

			this.WhenAnyValue(x => x.IsLocked)
				.Where(x => x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					inputField.Text = string.Empty;
					inputField.Focus();
				});
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			// When the control first created on AppStart set the Focus of the password box.
			// If you just simply set the Focus without delay it won't work.
			Observable
				.Interval(TimeSpan.FromSeconds(1))
				.Take(1)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					var inputField = this.FindControl<TogglePasswordBox>("InputField");
					inputField.Focus();
				});
		}
	}
}
