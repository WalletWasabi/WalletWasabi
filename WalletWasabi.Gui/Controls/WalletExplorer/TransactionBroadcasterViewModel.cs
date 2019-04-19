using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionBroadcasterViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		public TransactionBroadcasterViewModel(WalletViewModel walletViewModel) : base("Transaction Broadcaster", walletViewModel)
		{
		}

		public override void OnOpen()
		{
			if (Disposables != null)
			{
				throw new Exception("TransactionBroadcaster was opened before it was closed.");
			}

			Disposables = new CompositeDisposable();

			base.OnOpen();
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}
	}
}
