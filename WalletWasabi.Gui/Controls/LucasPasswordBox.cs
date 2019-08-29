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
	public class LucasPasswordBox : ExtendedTextBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(LucasPasswordBox);

		public static readonly StyledProperty<bool> IsPasswordVisibleProperty =
			AvaloniaProperty.Register<LucasPasswordBox, bool>(nameof(IsPasswordVisible), defaultBindingMode: BindingMode.TwoWay);

		public bool IsPasswordVisible
		{
			get => GetValue(IsPasswordVisibleProperty);
			set => SetValue(IsPasswordVisibleProperty, value);
		}

		public LucasPasswordBox()
		{
			this.GetObservable(IsPasswordVisibleProperty).Subscribe(x =>
			{
				IsPasswordVisible = x;
			});

			this.WhenAnyValue(x => x.IsPasswordVisible).Subscribe(x =>
			{
				PasswordChar = x ? '\0' : '*';
			});
		}

		protected override bool IsCopyEnabled => false;

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			var maskedButton = e.NameScope.Get<Button>("PART_MaskedButton");
			maskedButton.WhenAnyValue(x => x.IsPressed)
				.Where(x => x)
				.Subscribe(_ =>
				{
					IsPasswordVisible = !IsPasswordVisible;
				});
		}
	}
}
