using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewModel : ViewModelBase
	{
		private TransactionInfo _model;

		public TransactionViewModel(TransactionInfo model)
		{
			_model = model;

			_confirmed = model.WhenAnyValue(x => x.Confirmed).ToProperty(this, x => x.Confirmed, model.Confirmed);
		}

		private readonly ObservableAsPropertyHelper<bool> _confirmed;

		public string DateTime
		{
			get { return _model.DateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
		}

		public bool Confirmed
		{
			get { return _confirmed.Value; }
		}

		public string AmountBtc
		{
			get => _model.AmountBtc;
		}

		public Money Amount
		{
			get => Money.TryParse(_model.AmountBtc, out Money money) ? money : Money.Zero;
		}

		public string Label
		{
			get => _model.Label;
		}

		public string TransactionId
		{
			get => _model.TransactionId;
		}
	}
}
