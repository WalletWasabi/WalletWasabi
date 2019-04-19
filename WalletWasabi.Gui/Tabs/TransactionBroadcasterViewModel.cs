using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class TransactionBroadcasterViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		public TransactionBroadcasterViewModel() : base("TransactionBroadcaster")
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
