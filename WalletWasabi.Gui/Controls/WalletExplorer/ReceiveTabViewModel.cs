using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<AddressViewModel> _addresses;
		private AddressViewModel _selectedAddress;
		private string _label;
		private double _labelRequiredNotificationOpacity;
		private bool _labelRequiredNotificationVisible;
		private int _caretIndex;
		private ObservableCollection<SuggestionViewModel> _suggestions;

		public ReactiveCommand<Unit, Unit> CopyAddress { get; }
		public ReactiveCommand<Unit, Unit> CopyLabel { get; }
		public ReactiveCommand<Unit, Unit> ShowQrCode { get; }
		public ReactiveCommand<Unit, Unit> ChangeLabelCommand { get; }

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			_addresses = new ObservableCollection<AddressViewModel>();
			Label = "";

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				Label = Label.Trim(',', ' ').Trim();
				if (string.IsNullOrWhiteSpace(Label))
				{
					LabelRequiredNotificationVisible = true;
					LabelRequiredNotificationOpacity = 1;

					Dispatcher.UIThread.PostLogException(async () =>
					{
						await Task.Delay(TimeSpan.FromSeconds(4));
						LabelRequiredNotificationOpacity = 0;
					});

					return;
				}

				Dispatcher.UIThread.PostLogException(() =>
				{
					var label = Label;
					HdPubKey newKey = Global.WalletService.GetReceiveKey(label, Addresses.Select(x => x.Model).Take(7)); // Never touch the first 7 keys.

					AddressViewModel found = Addresses.FirstOrDefault(x => x.Model == newKey);
					if (found != default)
					{
						Addresses.Remove(found);
					}

					var newAddress = new AddressViewModel(newKey, Global);

					Addresses.Insert(0, newAddress);

					SelectedAddress = newAddress;

					Label = "";
				});
			});

			this.WhenAnyValue(x => x.Label).Subscribe(x => UpdateSuggestions(x));

			this.WhenAnyValue(x => x.SelectedAddress).Subscribe(async address =>
			{
				if (Global.UiConfig?.Autocopy is false || address is null)
				{
					return;
				}

				await address.TryCopyToClipboardAsync();
			});

			var isCoinListItemSelected = this.WhenAnyValue(x => x.SelectedAddress).Select(coin => coin != null);

			CopyAddress = ReactiveCommand.CreateFromTask(async () =>
			{
				if (SelectedAddress is null)
				{
					return;
				}

				await SelectedAddress.TryCopyToClipboardAsync();
			}, isCoinListItemSelected);

			CopyLabel = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					await Application.Current.Clipboard.SetTextAsync(SelectedAddress.Label ?? string.Empty);
				}
				catch (Exception)
				{ }
			}, isCoinListItemSelected);

			ShowQrCode = ReactiveCommand.Create(() =>
			{
				try
				{
					SelectedAddress.IsExpanded = true;
				}
				catch (Exception)
				{ }
			}, isCoinListItemSelected);

			ChangeLabelCommand = ReactiveCommand.Create(() =>
			{
				SelectedAddress.InEditMode = true;
			});

			_suggestions = new ObservableCollection<SuggestionViewModel>();
		}

		public override void OnOpen()
		{
			base.OnOpen();

			if (Disposables != null)
			{
				throw new Exception("Receive tab opened before last one was closed.");
			}

			Disposables = new CompositeDisposable();

			Observable.FromEventPattern(Global.WalletService.Coins,
				nameof(Global.WalletService.Coins.CollectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o => InitializeAddresses())
				.DisposeWith(Disposables);
		}

		public override bool OnClose()
		{
			Disposables.Dispose();

			Disposables = null;

			return base.OnClose();
		}

		private void InitializeAddresses()
		{
			_addresses?.Clear();
			var walletService = Global.WalletService;

			if (walletService is null)
			{
				return;
			}

			foreach (HdPubKey key in walletService.KeyManager.GetKeys(x =>
																		x.HasLabel
																		&& !x.IsInternal
																		&& x.KeyState == KeyState.Clean)
																	.Reverse())
			{
				_addresses.Add(new AddressViewModel(key, Global));
			}
		}

		public ObservableCollection<AddressViewModel> Addresses
		{
			get => _addresses;
			set => this.RaiseAndSetIfChanged(ref _addresses, value);
		}

		public AddressViewModel SelectedAddress
		{
			get => _selectedAddress;
			set => this.RaiseAndSetIfChanged(ref _selectedAddress, value);
		}

		public string Label
		{
			get => _label;
			set => this.RaiseAndSetIfChanged(ref _label, value);
		}

		public double LabelRequiredNotificationOpacity
		{
			get => _labelRequiredNotificationOpacity;
			set => this.RaiseAndSetIfChanged(ref _labelRequiredNotificationOpacity, value);
		}

		public bool LabelRequiredNotificationVisible
		{
			get => _labelRequiredNotificationVisible;
			set => this.RaiseAndSetIfChanged(ref _labelRequiredNotificationVisible, value);
		}

		public int CaretIndex
		{
			get => _caretIndex;
			set => this.RaiseAndSetIfChanged(ref _caretIndex, value);
		}

		public ObservableCollection<SuggestionViewModel> Suggestions
		{
			get => _suggestions;
			set => this.RaiseAndSetIfChanged(ref _suggestions, value);
		}

		private void UpdateSuggestions(string words)
		{
			if (string.IsNullOrWhiteSpace(words))
			{
				Suggestions?.Clear();
				return;
			}

			var enteredWordList = words.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
			var lastWord = enteredWordList?.LastOrDefault()?.Replace("\t", "") ?? "";

			if (!lastWord.Any())
			{
				Suggestions.Clear();
				return;
			}

			string[] nonSpecialLabels = Global.WalletService.GetNonSpecialLabels().ToArray();
			IEnumerable<string> suggestedWords = nonSpecialLabels.Where(w => w.StartsWith(lastWord, StringComparison.InvariantCultureIgnoreCase))
				.Union(nonSpecialLabels.Where(w => w.Contains(lastWord, StringComparison.InvariantCultureIgnoreCase)))
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

		public ReactiveCommand<Unit, Unit> GenerateCommand { get; }
	}
}
