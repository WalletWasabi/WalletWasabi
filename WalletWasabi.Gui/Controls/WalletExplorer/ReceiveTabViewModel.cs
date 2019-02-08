using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Tabs.EncryptionManager;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel, IDisposable
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
		private CompositeDisposable Disposables { get; }

		public ReactiveCommand GenerateCommand { get; }
		public ReactiveCommand EncryptMessage { get; }
		public ReactiveCommand DecryptMessage { get; }
		public ReactiveCommand SignMessage { get; }
		public ReactiveCommand VerifyMessage { get; }
		public ReactiveCommand CopyAddress { get; }

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			Disposables = new CompositeDisposable();
			_addresses = new ObservableCollection<AddressViewModel>();
			Label = "";

			Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
			{
				InitializeAddresses();
			}).DisposeWith(Disposables);

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
			{
				Label = Label.Trim(',', ' ').Trim();
				if (string.IsNullOrWhiteSpace(Label))
				{
					LabelRequiredNotificationVisible = true;
					LabelRequiredNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						try
						{
							await Task.Delay(1000);
							LabelRequiredNotificationOpacity = 0;
						}
						catch (Exception) { }
					});

					return;
				}

				Dispatcher.UIThread.Post(() =>
				{
					try
					{
						var label = Label;
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
					}
					catch (Exception) { }
				});
			}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Label).Subscribe(x => UpdateSuggestions(x)).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.SelectedAddress).Subscribe(address =>
			{
				if (!(address is null))
				{
					address.CopyToClipboard();
					ClipboardNotificationVisible = true;
					ClipboardNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						try
						{
							await Task.Delay(1000);
							ClipboardNotificationOpacity = 0;
						}
						catch (Exception) { }
					});
				}
			}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.CaretIndex).Subscribe(_ =>
			{
				if (Label == null) return;
				if (CaretIndex != Label.Length)
				{
					CaretIndex = Label.Length;
				}
			}).DisposeWith(Disposables);

			var isCoinListItemSelected = this.WhenAnyValue(x => x.SelectedAddress).Select(coin => coin != null);

			SignMessage = ReactiveCommand.Create(() =>
			{
				OnEncryptionManager(EncryptionManagerViewModel.Tabs.Sign, SelectedAddress.Address);
			}, isCoinListItemSelected)
			.DisposeWith(Disposables);

			VerifyMessage = ReactiveCommand.Create(() =>
			{
				OnEncryptionManager(EncryptionManagerViewModel.Tabs.Verify, SelectedAddress.Address);
			}, isCoinListItemSelected)
			.DisposeWith(Disposables);

			EncryptMessage = ReactiveCommand.Create(() =>
			{
				OnEncryptionManager(EncryptionManagerViewModel.Tabs.Encrypt, SelectedAddress.Model.PubKey.ToHex());
			}, isCoinListItemSelected)
			.DisposeWith(Disposables);

			DecryptMessage = ReactiveCommand.Create(() =>
			{
				OnEncryptionManager(EncryptionManagerViewModel.Tabs.Decrypt, SelectedAddress.Model.PubKey.ToHex());
			}, isCoinListItemSelected)
			.DisposeWith(Disposables);

			CopyAddress = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					await ((IClipboard)AvaloniaLocator.Current.GetService(typeof(IClipboard)))
						.SetTextAsync(SelectedAddress.Address ?? string.Empty);
				}
				catch (Exception)
				{ }
			}, isCoinListItemSelected)
			.DisposeWith(Disposables);

			_suggestions = new ObservableCollection<SuggestionViewModel>();
		}

		private void OnEncryptionManager(EncryptionManagerViewModel.Tabs selectedTab, string content)
		{
			var encryptionManagerViewModel = IoC.Get<IShell>().GetOrCreate<EncryptionManagerViewModel>();
			encryptionManagerViewModel.SelectTab(selectedTab, content);
		}

		private void InitializeAddresses()
		{
			_addresses?.Clear();

			foreach (HdPubKey key in Global.WalletService.KeyManager.GetKeys(x =>
																		x.HasLabel()
																		&& !x.IsInternal()
																		&& x.KeyState == KeyState.Clean)
																	.Reverse())
			{
				_addresses.Add(new AddressViewModel(key));
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

		public double ClipboardNotificationOpacity
		{
			get => _clipboardNotificationOpacity;
			set => this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value);
		}

		public bool ClipboardNotificationVisible
		{
			get => _clipboardNotificationVisible;
			set => this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value);
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

				_addresses = null;
				_suggestions = null;

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
