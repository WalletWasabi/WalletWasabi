using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class TextBoxInputLimitBehavior : DisposingBehavior<TextBox>
	{
		private int _pasteCharCountLimit;

		public static readonly DirectProperty<TextBoxInputLimitBehavior, int> PasteCharCountLimitProperty =
			AvaloniaProperty.RegisterDirect<TextBoxInputLimitBehavior, int>(nameof(PasteCharCountLimit),
				o => o.PasteCharCountLimit,
				(o,
					v) => o.PasteCharCountLimit = v, unsetValue: 150);

		public int PasteCharCountLimit
		{
			get => _pasteCharCountLimit;
			set => SetAndRaise(PasteCharCountLimitProperty, ref _pasteCharCountLimit, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			disposables?.Dispose();

			disposables = new CompositeDisposable();

			AssociatedObject?
				.AddDisposableHandler(TextBox.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
				.DisposeWith(disposables);

			AssociatedObject?
				.AddDisposableHandler(TextBox.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel)
				.DisposeWith(disposables);

			AssociatedObject?.WhenAnyValue(x=>x.Text)
				.Subscribe(OnTextChanged)
				.DisposeWith(disposables);
		}

		private void OnTextChanged(string o)
		{
			if (string.IsNullOrWhiteSpace(o))
			{
				return;
			}

			if (o.Length > PasteCharCountLimit)
			{
				Dispatcher.UIThread.Post(() =>
				{
					AssociatedObject.Text = o.Substring(0, PasteCharCountLimit);
				});
			}
		}

		private async void OnKeyDown(object? sender, KeyEventArgs e)
		{
			var keymap = AvaloniaLocator.Current.GetService<PlatformHotkeyConfiguration>();

			bool Match(List<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));

			if (Match(keymap.Paste))
			{
				var pasteText = await AvaloniaLocator.Current.GetService<IClipboard>().GetTextAsync();

				if (string.IsNullOrWhiteSpace(pasteText) || pasteText.Length == 1)
				{
					return;
				}

				if (pasteText.Length > PasteCharCountLimit)
				{
					AssociatedObject.RaiseEvent(new TextInputEventArgs
					{
						RoutedEvent = InputElement.TextInputEvent,
						Text = pasteText.Substring(0, PasteCharCountLimit)
					});
				}

				e.Handled = true;
			}
		}

		private void OnTextInput(object? sender, TextInputEventArgs e)
		{
			var txt = AssociatedObject.Text + e.Text ?? "";
			if (e.Handled || string.IsNullOrWhiteSpace(txt))
			{
				return;
			}

			e.Handled = txt.Length > PasteCharCountLimit;
		}
	}
}
