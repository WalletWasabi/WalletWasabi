using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using System;
using ReactiveUI;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls
{
	public class TogglePasswordBox : ExtendedTextBox, IStyleable
	{
		public static readonly StyledProperty<bool> IsPasswordVisibleProperty =
			AvaloniaProperty.Register<TogglePasswordBox, bool>(nameof(IsPasswordVisible), defaultBindingMode: BindingMode.TwoWay);

		public TogglePasswordBox()
		{
			UseFloatingWatermark = true;
			Watermark = "Password";
			MinWidth = 400;

			this.GetObservable(IsPasswordVisibleProperty)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsPasswordVisible = x);

			this.WhenAnyValue(x => x.IsPasswordVisible)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => PasswordChar = x ? '\0' : '\u2022');
		}

		Type IStyleable.StyleKey => typeof(TogglePasswordBox);

		public bool IsPasswordVisible
		{
			get => GetValue(IsPasswordVisibleProperty);
			set => SetValue(IsPasswordVisibleProperty, value);
		}

		protected override bool IsCopyEnabled => false;

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			var maskedButton = e.NameScope.Get<Button>("PART_MaskedButton");
			maskedButton.WhenAnyValue(x => x.IsPressed)
				.Where(x => x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					IsPasswordVisible = !IsPasswordVisible;

					// Refresh the already opened tooltip immediately.
					ToolTip.SetIsOpen(maskedButton, false);
					ToolTip.SetIsOpen(maskedButton, true);
				});
		}
	}
}
