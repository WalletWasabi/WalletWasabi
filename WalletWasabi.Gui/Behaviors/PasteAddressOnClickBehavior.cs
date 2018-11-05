using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using NBitcoin;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	internal class PasteAddressOnClickBehavior : Behavior<TextBox>
	{
		private CompositeDisposable _disposables = new CompositeDisposable();

		public void PasteClipboardContentIfBitcoinAddress()
		{
			var clipboard = (IClipboard)AvaloniaLocator.Current.GetService(typeof(IClipboard)); 
			var text = clipboard.GetTextAsync().GetAwaiter().GetResult();

			try
			{
				var address = BitcoinAddress.Create(text, Global.Network);
				if(address is BitcoinWitPubKeyAddress)
				{
					if(string.IsNullOrWhiteSpace(AssociatedObject.Text) )
						AssociatedObject.Text = text;
				}
			}
			catch(FormatException)
			{
			}
		} 

		protected override void OnAttached()
		{
			_disposables = new CompositeDisposable
			{
				Observable.FromEventPattern<RoutedEventArgs>(AssociatedObject, nameof(AssociatedObject.LostFocus)).Subscribe(args=>
				{
					PasteClipboardContentIfBitcoinAddress();
				})
			};

			base.OnAttached();
 		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposables.Dispose();
		}
	}
}
