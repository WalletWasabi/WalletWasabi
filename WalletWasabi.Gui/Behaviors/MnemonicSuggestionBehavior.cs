using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	internal class MnemonicSuggestionBehavior : Behavior<TextBox>
	{
		private CompositeDisposable _disposables;

		private static readonly AvaloniaProperty<string> SuggestionItemsProperty =
			AvaloniaProperty.Register<MnemonicSuggestionBehavior, string>(nameof(SuggestionItems), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

		public string SuggestionItems
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
			var textBox = AssociatedObject;
			var text = textBox.Text;
			var enteredWordList = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var lastWorld = enteredWordList.LastOrDefault();
			var suggestions = SuggestionItems.Split(" ", StringSplitOptions.RemoveEmptyEntries);
			if(suggestions.Length == 1)
			{
				textBox.Text = text.Substring(0, text.Length - lastWorld.Length) + suggestions[0] + " ";
				textBox.CaretIndex = textBox.Text.Length;
			}
		}
	}
}