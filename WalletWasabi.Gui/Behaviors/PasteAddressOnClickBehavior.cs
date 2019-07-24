using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls;

namespace WalletWasabi.Gui.Behaviors
{
	internal class PasteAddressOnClickBehavior : CommandBasedBehavior<TextBox>
	{
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }

		protected internal enum TextBoxState
		{
			None,
			NormalTextBoxOperation,
			AddressInsert,
			SelectAll,
		}

		private TextBoxState MyTextBoxState
		{
			get => _textBoxState;
			set
			{
				_textBoxState = value;
				switch (value)
				{
					case TextBoxState.NormalTextBoxOperation:
						{
							AssociatedObject.Cursor = new Cursor(StandardCursorType.Ibeam);
						}
						break;

					case TextBoxState.AddressInsert:
						{
							AssociatedObject.Cursor = new Cursor(StandardCursorType.Arrow);
						}
						break;

					case TextBoxState.SelectAll:
						{
							AssociatedObject.Cursor = new Cursor(StandardCursorType.Arrow);
						}
						break;
				}
			}
		}

		private TextBoxState _textBoxState = TextBoxState.None;

		private bool TryParse(string text, out BitcoinUrlBuilder result)
		{
			result = null;
			if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
			{
				return false;
			}

			text = text.Trim();
			var addressText = IsBitcoinAddress(text, Global.Network);
			if (addressText.isValid)
			{
				result = addressText.url;
				return true;
			}
			else
			{
				var urlText = IsBitcoinUrl(text, Global.Network);
				if (urlText.isValid)
				{
					result = urlText.url;
					return true;
				}
			}
			return false;
		}

		private bool ProcessText(string text)
		{
			if (TryParse(text, out BitcoinUrlBuilder result))
			{
				AssociatedObject.Text = result?.Address?.ToString();
				CommandParameter = result;
				ExecuteCommand();
				return true;
			}

			return false;
		}

		private (bool isValid, BitcoinUrlBuilder url) IsBitcoinAddress(string text, Network expectedNetwork)
		{
			if (text.Length > 100)
			{
				return (false, null);
			}

			try
			{
				var bitcoinAddress = BitcoinAddress.Create(text, expectedNetwork);
				return (true, new BitcoinUrlBuilder($"bitcoin:{bitcoinAddress}"));
			}
			catch (FormatException)
			{
				return (false, null);
			}
		}

		private (bool isValid, BitcoinUrlBuilder url) IsBitcoinUrl(string text, Network expectedNetwork)
		{
			try
			{
				var bitcoinUrl = new BitcoinUrlBuilder(text);
				return (bitcoinUrl.Address.Network == expectedNetwork, bitcoinUrl);
			}
			catch (FormatException)
			{
				return (false, null);
			}
		}

		public PasteAddressOnClickBehavior()
		{
			Global = Application.Current.Resources[Global.GlobalResourceKey] as Global;
		}

		protected override void OnAttached()
		{
			Disposables?.Dispose();

			Disposables = new CompositeDisposable
			{
				AssociatedObject.GetObservable(InputElement.IsFocusedProperty).Subscribe(focused =>
				{
					if (!focused)
					{
						MyTextBoxState = TextBoxState.None;
					}
				}),
				AssociatedObject
					.GetObservable(InputElement.KeyUpEvent)
					.Throttle(TimeSpan.FromMilliseconds(500)) // Do not remove this we need to make sure we are running on a separate Task.
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(_ =>
					{
						ProcessText(AssociatedObject.Text);
						MyTextBoxState = TextBoxState.NormalTextBoxOperation;
					})
			};

			if (AssociatedObject is ExtendedTextBox extendedTextBox)
			{
				Disposables.Add(extendedTextBox.TextPasted
					.Subscribe(text =>
					{
						ProcessText(text);
						MyTextBoxState = TextBoxState.NormalTextBoxOperation;
					}));
			}

			Disposables.Add(
				AssociatedObject.GetObservable(InputElement.PointerReleasedEvent).Subscribe(async pointer =>
				{
					if (Global.UiConfig.Autocopy is null || Global.UiConfig.Autocopy is false)
					{
						return;
					}

					switch (MyTextBoxState)
					{
						case TextBoxState.AddressInsert:
							{
								string text = await Application.Current.Clipboard.GetTextAsync();
								ProcessText(text);
								MyTextBoxState = TextBoxState.NormalTextBoxOperation;
							}
							break;

						case TextBoxState.SelectAll:
							{
								AssociatedObject.SelectionStart = 0;
								AssociatedObject.SelectionEnd = AssociatedObject.Text.Length;
								MyTextBoxState = TextBoxState.NormalTextBoxOperation;
							}
							break;
					}
				})
			);

			Disposables.Add(
				AssociatedObject.GetObservable(InputElement.PointerEnterEvent).Subscribe(async pointerEnter =>
				{
					if (!(Global.UiConfig.Autocopy is true))
					{
						return;
					}

					if (!AssociatedObject.IsFocused && MyTextBoxState == TextBoxState.NormalTextBoxOperation)
					{
						MyTextBoxState = TextBoxState.None;
					}

					if (MyTextBoxState == TextBoxState.NormalTextBoxOperation)
					{
						return;
					}

					if (string.IsNullOrEmpty(AssociatedObject.Text))
					{
						string text = await Application.Current.Clipboard.GetTextAsync();

						if (TryParse(text, out _))
						{
							MyTextBoxState = TextBoxState.AddressInsert;
						}
						else
						{
							MyTextBoxState = TextBoxState.NormalTextBoxOperation;
						}
					}
					else
					{
						MyTextBoxState = TextBoxState.SelectAll;
					}
				})
			);

			base.OnAttached();
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
