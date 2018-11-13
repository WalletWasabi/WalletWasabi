using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<AddressViewModel> _addresses;
		private AddressViewModel _selectedAddress;
		private string _label;
		private double _labelRequiredNotificationOpacity;
		private bool _labelRequiredNotificationVisible;
		private double _clipboardNotificationOpacity;
		private bool _clipboardNotificationVisible;
		private int _caretIndex;
		private ObservableCollection<SuggestionViewModel> _suggestions;

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			_addresses = new ObservableCollection<AddressViewModel>();
			Label = "";

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
			{
				InitializeAddresses();
			});

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				if (string.IsNullOrWhiteSpace(Label))
				{
					LabelRequiredNotificationVisible = true;
					LabelRequiredNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						await Task.Delay(1000);
						LabelRequiredNotificationOpacity = 0;
					});

					return;
				}

				Dispatcher.UIThread.Post(() =>
				{
					var label = Label.Trim().Trim(',').Trim();
					HdPubKey newKey = Global.WalletService.GetReceiveKey(label, Addresses.Select(x => x.Model).Take(7)); // Never touch the first 7 keys.

					AddressViewModel found = Addresses.FirstOrDefault(x => x.Model == newKey);
					if (found != default)
					{
						Addresses.Remove(found);
					}

					var newAddress = new AddressViewModel(newKey);

					Addresses.Insert(0, newAddress);

					SelectedAddress = newAddress;

					Label = "";
				});
			});
			this.WhenAnyValue(x => x.Label).Subscribe(x => UpdateSuggestions(x));
			this.WhenAnyValue(x => x.SelectedAddress).Subscribe(address =>
			{
				if (!(address is null))
				{
					address.CopyToClipboard();
					ClipboardNotificationVisible = true;
					ClipboardNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						await Task.Delay(1000);
						ClipboardNotificationOpacity = 0;
					});
				}
			});

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

		private void InitializeAddresses()
		{
			_addresses?.Clear();

			var keys = Global.WalletService.KeyManager.GetKeys(KeyState.Clean, false);

			foreach (HdPubKey key in keys.Where(x => x.HasLabel()).Reverse())
			{
				_addresses.Add(new AddressViewModel(key));
			}
		}

		public ObservableCollection<AddressViewModel> Addresses
		{
			get { return _addresses; }
			set { this.RaiseAndSetIfChanged(ref _addresses, value); }
		}

		public AddressViewModel SelectedAddress
		{
			get { return _selectedAddress; }
			set { this.RaiseAndSetIfChanged(ref _selectedAddress, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public double LabelRequiredNotificationOpacity
		{
			get { return _labelRequiredNotificationOpacity; }
			set { this.RaiseAndSetIfChanged(ref _labelRequiredNotificationOpacity, value); }
		}

		public bool LabelRequiredNotificationVisible
		{
			get { return _labelRequiredNotificationVisible; }
			set { this.RaiseAndSetIfChanged(ref _labelRequiredNotificationVisible, value); }
		}

		public double ClipboardNotificationOpacity
		{
			get { return _clipboardNotificationOpacity; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value); }
		}

		public bool ClipboardNotificationVisible
		{
			get { return _clipboardNotificationVisible; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value); }
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

		public ReactiveCommand GenerateCommand { get; }
	}
}
