using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _coinList;
		private string _buildTransactionButtonText;
		private bool _isMax;
		private string _maxClear;
		private string _amount;
		private bool IgnoreAmountChanges { get; set; }
		private int _fee;
		private string _password;
		private string _address;
		private string _label;
		private bool _isBusy;
		private string _warningMessage;
		private string _successMessage;
		private const string BuildTransactionButtonTextString = "Send Transaction";
		private const string BuildingTransactionButtonTextString = "Sending Transaction...";

		public SendTabViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel)
		{
			var onCoinsSetModified = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged))
				.ObserveOn(RxApp.MainThreadScheduler);

			var globalCoins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(c), null, (first, second) => second.Amount.CompareTo(first.Amount), signalReset: onCoinsSetModified, RxApp.MainThreadScheduler);
			globalCoins.ChangeTrackingEnabled = true;

			var filteredCoins = globalCoins.CreateDerivedCollection(c => c, c => !c.SpentOrCoinJoinInProgress);

			CoinList = new CoinListViewModel(filteredCoins);

			BuildTransactionButtonText = BuildTransactionButtonTextString;

			ResetMax();

			this.WhenAnyValue(x => x.Amount).Subscribe(amount =>
			{
				if (!IgnoreAmountChanges)
				{
					IsMax = false;

					// Correct amount
					Regex digitsOnly = new Regex(@"[^\d,.]");
					string betterAmount = digitsOnly.Replace(amount, ""); // Make it digits , and . only.
					betterAmount = betterAmount.Replace(',', '.');
					int countBetterAmount = betterAmount.Count(x => x == '.');
					if (countBetterAmount > 1) // Don't enable typing two dots.
					{
						var index = betterAmount.IndexOf('.', betterAmount.IndexOf('.') + 1);
						if (index > 0)
						{
							betterAmount = betterAmount.Substring(0, index);
						}
					}
					var dotIndex = betterAmount.IndexOf('.');
					if (betterAmount.Length - dotIndex > 8) // Enable max 8 decimals.
					{
						betterAmount = betterAmount.Substring(0, dotIndex + 1 + 8);
					}

					if (betterAmount != amount)
					{
						Dispatcher.UIThread.Post(() =>
						{
							Amount = betterAmount;
						});
					}
				}
			});

			BuildTransactionCommand = ReactiveCommand.Create(async () =>
			{
				IsBusy = true;
				try
				{
					Password = Guard.Correct(Password);
					if (string.IsNullOrWhiteSpace(Label))
					{
						throw new InvalidOperationException("Label is required.");
					}

					var selectedCoins = CoinList.Coins.Where(cvm => cvm.IsSelected).Select(cvm => new TxoRef(cvm.Model.TransactionId, cvm.Model.Index)).ToList();

					if (!selectedCoins.Any())
					{
						throw new InvalidOperationException("No coins are selected to spend.");
					}

					var address = BitcoinAddress.Create(Address.Trim(), Global.Network);
					var script = address.ScriptPubKey;
					var amount = Money.Zero;
					if (!IsMax)
					{
						amount = Money.Parse(Amount);
						if (amount == Money.Zero)
						{
							throw new FormatException($"Invalid {nameof(Amount)}");
						}
					}
					var operation = new WalletService.Operation(script, amount, Label);

					var result = await Task.Run(async () => await Global.WalletService.BuildTransactionAsync(Password, new[] { operation }, Fee, allowUnconfirmed: true, allowedInputs: selectedCoins));

					await Task.Run(async () => await Global.WalletService.SendTransactionAsync(result.Transaction));

					ResetMax();
					Address = "";
					Label = "";
					Password = "";

					SuccessMessage = "Transaction is successfully sent!";
					WarningMessage = "";
				}
				catch (Exception ex)
				{
					SuccessMessage = "";
					WarningMessage = ex.ToTypeMessageString();
				}
				finally
				{
					IsBusy = false;
				}
			},
			this.WhenAny(x => x.IsMax, x => x.Amount, x => x.Address, x => x.IsBusy,
				(isMax, amount, address, busy) => ((isMax.Value || !string.IsNullOrWhiteSpace(amount.Value)) && !string.IsNullOrWhiteSpace(Address) && !IsBusy)));

			MaxCommand = ReactiveCommand.Create(() =>
			{
				SetMax();
			});

			this.WhenAnyValue(x => x.IsBusy).Subscribe(busy =>
			{
				if (busy)
				{
					BuildTransactionButtonText = BuildingTransactionButtonTextString;
				}
				else
				{
					BuildTransactionButtonText = BuildTransactionButtonTextString;
				}
			});

			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				if (x.NotNullAndNotEmpty())
				{
					char lastChar = x.Last();
					if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
					{
						Password = x.TrimEnd('\r', '\n');
					}
				}
			});
		}

		private void SetMax()
		{
			if (IsMax)
			{
				ResetMax();
				return;
			}

			IsMax = true;
			MaxClear = "Clear";

			IgnoreAmountChanges = true;
			Amount = "All Selected Coins!";
			IgnoreAmountChanges = false;
		}

		private void ResetMax()
		{
			IsMax = false;
			MaxClear = "Max";

			IgnoreAmountChanges = true;
			Amount = "0.0";
			IgnoreAmountChanges = false;
		}

		public CoinListViewModel CoinList
		{
			get { return _coinList; }
			set { this.RaiseAndSetIfChanged(ref _coinList, value); }
		}

		public bool IsBusy
		{
			get { return _isBusy; }
			set { this.RaiseAndSetIfChanged(ref _isBusy, value); }
		}

		public string BuildTransactionButtonText
		{
			get { return _buildTransactionButtonText; }
			set { this.RaiseAndSetIfChanged(ref _buildTransactionButtonText, value); }
		}

		public bool IsMax
		{
			get { return _isMax; }
			set { this.RaiseAndSetIfChanged(ref _isMax, value); }
		}

		public string MaxClear
		{
			get { return _maxClear; }
			set { this.RaiseAndSetIfChanged(ref _maxClear, value); }
		}

		public string Amount
		{
			get { return _amount; }
			set { this.RaiseAndSetIfChanged(ref _amount, value); }
		}

		public int Fee
		{
			get { return _fee; }
			set { this.RaiseAndSetIfChanged(ref _fee, value); }
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public string ValidateAddress()
		{
			if (string.IsNullOrEmpty(Address))
			{
				return "";
			}

			if (!string.IsNullOrWhiteSpace(Address))
			{
				var trimmed = Address.Trim();
				try
				{
					BitcoinAddress.Create(trimmed, Global.Network);
					return "";
				}
				catch
				{
				}
			}

			return $"Invalid {nameof(Address)}";
		}

		[ValidateMethod(nameof(ValidateAddress))]
		public string Address
		{
			get { return _address; }
			set { this.RaiseAndSetIfChanged(ref _address, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public string WarningMessage
		{
			get { return _warningMessage; }
			set { this.RaiseAndSetIfChanged(ref _warningMessage, value); }
		}

		public string SuccessMessage
		{
			get { return _successMessage; }
			set { this.RaiseAndSetIfChanged(ref _successMessage, value); }
		}

		public ReactiveCommand BuildTransactionCommand { get; }

		public ReactiveCommand MaxCommand { get; }
	}
}
