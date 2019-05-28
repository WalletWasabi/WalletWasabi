using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using NBitcoin;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Behaviors
{
	internal class PasteAddressOnClickBehavior : Behavior<TextBox>
	{
		private CompositeDisposable Disposables { get; set; }

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
							AssociatedObject.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Ibeam);
						}
						break;

					case TextBoxState.AddressInsert:
						{
							AssociatedObject.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
						}
						break;

					case TextBoxState.SelectAll:
						{
							AssociatedObject.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
						}
						break;
				}
			}
		}

		private TextBoxState _textBoxState = TextBoxState.None;

		public async Task<(bool isAddress, string address)> IsThereABitcoinAddressOnTheClipboardAsync()
		{
			string text = await Application.Current.Clipboard.GetTextAsync();
			if (string.IsNullOrEmpty(text) || text.Length > 100)
			{
				return (false, null);
			}

			text = text.Trim();
			try
			{
				var bitcoinAddress = BitcoinAddress.Create(text, Global.Network);
				return (true, bitcoinAddress.ToString());
			}
			catch (FormatException)
			{
				return (false, null);
			}
		}

		protected override void OnAttached()
		{
			Disposables?.Dispose();

			Disposables = new CompositeDisposable
			{
				AssociatedObject.GetObservable(TextBox.IsFocusedProperty).Subscribe(focused =>
				{
					if (!focused)
					{
						MyTextBoxState = TextBoxState.None;
					}
				})
			};

			Disposables.Add(
				AssociatedObject.GetObservable(TextBox.PointerReleasedEvent).Subscribe(async pointer =>
				{
					switch (MyTextBoxState)
					{
						case TextBoxState.AddressInsert:
							var result = await IsThereABitcoinAddressOnTheClipboardAsync();

							if (result.isAddress)
							{
								AssociatedObject.Text = result.address;
							}
							MyTextBoxState = TextBoxState.NormalTextBoxOperation;
							var labeltextbox = AssociatedObject.Parent.FindControl<TextBox>("LabelTextBox");
							if (labeltextbox != null)
							{
								labeltextbox.Focus();
							}

							break;

						case TextBoxState.SelectAll:
							AssociatedObject.SelectionStart = 0;
							AssociatedObject.SelectionEnd = AssociatedObject.Text.Length;
							MyTextBoxState = TextBoxState.NormalTextBoxOperation;
							break;
					}
				})
			);

			Disposables.Add(
				AssociatedObject.GetObservable(TextBox.PointerEnterEvent).Subscribe(async pointerEnter =>
				{
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
						var result = await IsThereABitcoinAddressOnTheClipboardAsync();
						if (result.isAddress)
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
