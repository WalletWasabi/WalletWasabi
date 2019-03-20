using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase
	{
		private TransactionInfo _model;

		public TransactionViewModel(TransactionInfo model)
		{
			_model = model;
			_confirmed = model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed, model.Confirmed).DisposeWith(Disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).Subscribe(x =>
			{
				this.RaisePropertyChanged(nameof(AmountBtc));
				this.RaisePropertyChanged(nameof(TransactionId));
				this.RaisePropertyChanged(nameof(DateTime));
			}).DisposeWith(Disposables);
		}

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(AmountBtc));
			this.RaisePropertyChanged(nameof(TransactionId));
			this.RaisePropertyChanged(nameof(DateTime));
		}

		public string DateTime => _model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		public bool Confirmed => _model.Confirmed;

		public string AmountBtc => _model.AmountBtc;

		public Money Amount => Money.TryParse(_model.AmountBtc, out Money money) ? money : Money.Zero;

		public string Label => _model.Label;

		public string TransactionId => _model.TransactionId;

		public void CopyToClipboard()
		{
			Application.Current.Clipboard.SetTextAsync(TransactionId).GetAwaiter().GetResult();

			Global.NotificationManager.Notify(NotificationTypeEnum.Success, "Transaction copied to the clipboard");
		}
	}
}
