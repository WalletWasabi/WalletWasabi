using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using NBitcoin;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.ViewModels
{
	public class AddressPubKeyViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; }
		private HdPubKey _hdPubKey;

		public string PubKey
		{
			get => _hdPubKey.PubKey.ToHex();
		}

		public string Label
		{
			get => _hdPubKey.Label;
		}

		public string Address
		{
			get => _hdPubKey.GetP2wpkhAddress(Global.Network).ToString();
		}

		public AddressPubKeyViewModel(HdPubKey hdpubkey)
		{
			_hdPubKey = hdpubkey;
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}
				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
