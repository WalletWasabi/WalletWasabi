using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls
{
	public class LucasPasswordBox : ExtendedTextBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(LucasPasswordBox);
		private Button _presenter;

		public static readonly StyledProperty<bool> IsPasswordVisibleProperty =
			AvaloniaProperty.Register<NoparaPasswordBox, bool>(nameof(IsPasswordVisible), defaultBindingMode: BindingMode.TwoWay);

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
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);
			_presenter = e.NameScope.Get<Button>("PART_MaskedButton");

			_presenter.WhenAnyValue(x => x.IsPressed)
				.Subscribe(isPressed =>
				{
					IsPasswordVisible = isPressed;
				});
		}
	}
}
