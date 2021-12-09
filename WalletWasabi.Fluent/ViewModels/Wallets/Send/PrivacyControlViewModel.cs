using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;
		private readonly bool _isSilent;
		private readonly SourceList<PocketViewModel> _pocketSource;
		private readonly ReadOnlyObservableCollection<PocketViewModel> _pockets;
		private readonly IObservableList<PocketViewModel> _selectedPockets;

		[AutoNotify] private decimal _stillNeeded;
		[AutoNotify] private bool _enoughSelected;
		[AutoNotify] private bool _isWarningOpen;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
		{
			_wallet = wallet;
			_transactionInfo = transactionInfo;
			_isSilent = isSilent;

			_pocketSource = new SourceList<PocketViewModel>();

			_pocketSource.Connect()
				.Bind(out _pockets)
				.Subscribe();

			var selected = _pocketSource.Connect()
				.AutoRefresh()
				.Filter(x => x.IsSelected);

			_selectedPockets = selected.AsObservableList();

			selected.Sum(x => x.TotalBtc)
				.Subscribe(x =>
				{
					IsWarningOpen = _selectedPockets.Count > 1 && _selectedPockets.Items.Any(x => x.Labels == CoinPocketHelper.PrivateFundsText);

					StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC) - x;
					EnoughSelected = StillNeeded <= 0;
				});

			StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;

			NextCommand = ReactiveCommand.Create(Complete, this.WhenAnyValue(x => x.EnoughSelected));

			EnableAutoBusyOn(NextCommand);
		}

		public ReadOnlyObservableCollection<PocketViewModel> Pockets => _pockets;

		private void Complete()
		{
			Close(DialogResultKind.Normal, _selectedPockets.Items.SelectMany(x => x.Coins).ToArray());
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			if (!isInHistory)
			{
				var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue()).Select(x => new PocketViewModel(x));

				_pocketSource.AddRange(pockets);
			}

			foreach (var pocket in _pockets)
			{
				pocket.IsSelected = false;
			}

			if (_pocketSource.Count == 1)
			{
				_pocketSource.Items.First().IsSelected = true;

				Complete();
			}
			else if (_isSilent &&
					 _pocketSource.Items.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket &&
					 privatePocket.Coins.TotalAmount() >= _transactionInfo.Amount)
			{
				privatePocket.IsSelected = true;
				Complete();
			}
		}
	}
}
