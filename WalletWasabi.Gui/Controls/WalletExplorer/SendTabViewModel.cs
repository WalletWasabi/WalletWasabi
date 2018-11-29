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
using ReactiveUI.Legacy;
using WalletWasabi.Exceptions;
using System.Collections.ObjectModel;
using WalletWasabi.Gui.Tabs.WalletManager;

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
		private string _labelToolTip;
		private bool _isBusy;
		private string _warningMessage;
		private string _successMessage;
		private const string BuildTransactionButtonTextString = "Send Transaction";
		private const string BuildingTransactionButtonTextString = "Sending Transaction...";
		private int _caretIndex;
		private ObservableCollection<SuggestionViewModel> _suggestions;

		public SendTabViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel)
		{
			Label = "";

			CoinList = new CoinListViewModel();

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
					if (!IsMax && string.IsNullOrWhiteSpace(Label))
					{
						SetWarningMessage("Label is required.");
						return;
					}

					var selectedCoinViewModels = CoinList.Coins.Where(cvm => cvm.IsSelected);
					var selectedCoinReferences = selectedCoinViewModels.Select(cvm => new TxoRef(cvm.Model.TransactionId, cvm.Model.Index)).ToList();

					if (!selectedCoinReferences.Any())
					{
						SetWarningMessage("No coins are selected to spend.");
						return;
					}

					BitcoinAddress address;
					try
					{
						address = BitcoinAddress.Create(Address.Trim(), Global.Network);
					}
					catch (FormatException)
					{
						SetWarningMessage("Invalid address.");
						return;
					}

					var script = address.ScriptPubKey;
					var amount = Money.Zero;
					if (!IsMax)
					{
						amount = Money.Parse(Amount);
						if (amount == Money.Zero)
						{
							SetWarningMessage($"Invalid amount.");
							return;
						}
					}
					var label = Label.Trim(',', ' ').Trim();
					var operation = new WalletService.Operation(script, amount, label);

					try
					{
						await Global.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoinReferences.Select(x => (x.TransactionId, x.Index)).ToArray());
					}
					catch
					{
						SetWarningMessage("Spending coins those are being actively mixed is not allowed.");
						return;
					}

					var result = await Task.Run(async () => await Global.WalletService.BuildTransactionAsync(Password, new[] { operation }, Fee, allowUnconfirmed: true, allowedInputs: selectedCoinReferences));

					await Task.Run(async () => await Global.WalletService.SendTransactionAsync(result.Transaction));

					ResetMax();
					Address = "";
					Label = "";
					Password = "";

					SetSuccessMessage("Transaction is successfully sent!");
				}
				catch (InsufficientBalanceException ex)
				{
					Money needed = ex.Minimum - ex.Actual;
					SetWarningMessage($"Not enough coins selected. You need an estimated {needed.ToString(false, true)} BTC more to make this transaction.");
				}
				catch (Exception ex)
				{
					SetWarningMessage(ex.ToTypeMessageString());
				}
				finally
				{
					IsBusy = false;
				}
			},
			(this).WhenAny(x => x.IsMax, x => x.Amount, x => x.Address, x => x.IsBusy,
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

			this.WhenAnyValue(x => x.Label).Subscribe(x => UpdateSuggestions(x));
			this.WhenAnyValue(x => x.CaretIndex).Subscribe(_ =>
			{
				if (Label == null) return;
				if (CaretIndex != Label.Length)
				{
					CaretIndex = Label.Length;
				}
			});
			_suggestions = new ObservableCollection<SuggestionViewModel>();
		}

		private void SetWarningMessage(string message)
		{
			SuccessMessage = "";
			WarningMessage = message;

			Dispatcher.UIThread.Post(async () =>
			{
				await Task.Delay(7000);
				if (WarningMessage == message)
				{
					WarningMessage = "";
				}
			});
		}

		private void SetSuccessMessage(string message)
		{
			SuccessMessage = message;
			WarningMessage = "";

			Dispatcher.UIThread.Post(async () =>
			{
				await Task.Delay(7000);
				if (SuccessMessage == message)
				{
					SuccessMessage = "";
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

			LabelToolTip = "Spending whole coins doesn't generate change, thus labeling is unnecessary.";
		}

		private void ResetMax()
		{
			IsMax = false;
			MaxClear = "Max";

			IgnoreAmountChanges = true;
			Amount = "0.0";
			IgnoreAmountChanges = false;

			LabelToolTip = "Start labelling today and your privacy will thank you tomorrow!";
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

		public int CaretIndex
		{
			get { return _caretIndex; }
			set { this.RaiseAndSetIfChanged(ref _caretIndex, value); }
		}

		public ObservableCollection<SuggestionViewModel> Suggestions
		{
			get { return _suggestions; }
			set { this.RaiseAndSetIfChanged(ref _suggestions, value); }
		}

		private void UpdateSuggestions(string words)
		{
			if (string.IsNullOrWhiteSpace(words))
			{
				Suggestions?.Clear();
				return;
			}

			var enteredWordList = words.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
			var lastWorld = enteredWordList.LastOrDefault().Replace("\t", "");

			if (lastWorld.Length < 1)
			{
				Suggestions.Clear();
				return;
			}

			string[] nonSpecialLabels = Global.WalletService.GetNonSpecialLabels().ToArray();
			IEnumerable<string> suggestedWords = nonSpecialLabels.Where(w => w.StartsWith(lastWorld, StringComparison.InvariantCultureIgnoreCase))
				.Union(nonSpecialLabels.Where(w => w.Contains(lastWorld, StringComparison.InvariantCultureIgnoreCase)))
				.Except(enteredWordList)
				.Take(3);

			Suggestions.Clear();
			foreach (var suggestion in suggestedWords)
			{
				Suggestions.Add(new SuggestionViewModel(suggestion, OnAddWord));
			}
		}

		public void OnAddWord(string word)
		{
			var words = Label.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
			if (words.Length == 0)
			{
				Label = word + ", ";
			}
			else
			{
				words[words.Length - 1] = word;
				Label = string.Join(", ", words) + ", ";
			}

			CaretIndex = Label.Length;

			Suggestions.Clear();
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

		public string LabelToolTip
		{
			get { return _labelToolTip; }
			set { this.RaiseAndSetIfChanged(ref _labelToolTip, value); }
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
