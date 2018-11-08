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
		private CompositeDisposable _disposables = new CompositeDisposable();

		public void PasteClipboardContentIfBitcoinAddress()
		{
			if(IsThereABitcoinAddressOnTheClipboard(out string address))
			{
				if(string.IsNullOrWhiteSpace(AssociatedObject.Text) )
					AssociatedObject.Text = address;
			}
		} 

		public bool IsThereABitcoinAddressOnTheClipboard(out string address)
		{
			address = string.Empty;

			var clipboard = (IClipboard)AvaloniaLocator.Current.GetService(typeof(IClipboard));	
			
			// TODO: fix this
			var clipboardTask = clipboard.GetTextAsync();
			Task.WaitAny(Task.Delay(10), clipboardTask);
			if(!clipboardTask.IsCompleted)
			{
				return false;
			}

			var text = clipboardTask.Result;
			try
			{
				var bitcoinAddress = BitcoinAddress.Create(text, Global.Network);
				address = text;
				return bitcoinAddress is BitcoinWitPubKeyAddress;
			}
			catch(FormatException)
			{
				return false;
			}
		}

		protected override void OnAttached()
		{
			_disposables = new CompositeDisposable
			{
				AssociatedObject.GetObservable(TextBox.IsFocusedProperty).Subscribe(focused =>
				{
					if(focused)
					{
						if(string.IsNullOrWhiteSpace(AssociatedObject.Text))
						{
							PasteClipboardContentIfBitcoinAddress();
						}
					}
				})				
			};

			_disposables.Add(
				AssociatedObject.GetObservable(TextBox.PointerReleasedEvent).Subscribe(pointer =>
				{
					if(!string.IsNullOrWhiteSpace(AssociatedObject.Text))
					{
						AssociatedObject.SelectionStart = 0;
						AssociatedObject.SelectionEnd = AssociatedObject.Text?.Length ?? 0;
					}
				})
			);

			_disposables.Add(
				AssociatedObject.GetObservable(TextBox.PointerEnterEvent).Subscribe(pointerEnter =>
				{
					if(IsThereABitcoinAddressOnTheClipboard(out string address))
					{
						ToolTip.SetTip(AssociatedObject, "Click to paste address from clipboard");
					}
					else
					{
						ToolTip.SetTip(AssociatedObject, "");
					}
				})
			);

			base.OnAttached();
 		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposables.Dispose();
		}
	}
}
