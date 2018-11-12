using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Behaviors
{
	internal class SuggestionBehavior : Behavior<TextBox>
	{
		private CompositeDisposable _disposables;

		private static readonly AvaloniaProperty<IEnumerable<SuggestionViewModel>> SuggestionItemsProperty =
			AvaloniaProperty.Register<SuggestionBehavior, IEnumerable<SuggestionViewModel>>(nameof(SuggestionItems), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

		public IEnumerable<SuggestionViewModel> SuggestionItems
		{
			get => GetValue(SuggestionItemsProperty);
			set => SetValue(SuggestionItemsProperty, value);
		}

		protected override void OnAttached()
		{
			_disposables = new CompositeDisposable();

			base.OnAttached();

			_disposables.Add(AssociatedObject.AddHandler(TextBox.KeyDownEvent, (sender, e) =>
			{
				if (e.Key == Avalonia.Input.Key.Tab)
				{
					HandleAutoUpdate();
					e.Handled = true;
				}
			}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposables.Dispose();
		}

		private void HandleAutoUpdate()
		{
			SuggestionItems.FirstOrDefault()?.OnSelected();
		}
	}
}