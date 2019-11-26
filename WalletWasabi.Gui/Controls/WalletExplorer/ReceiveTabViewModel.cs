using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using Splat;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<AddressViewModel> _addresses;
		private AddressViewModel _selectedAddress;
		private SuggestLabelViewModel _labelSuggestion;
		private Global Global { get; }

		public ReactiveCommand<Unit, Unit> CopyAddress { get; }
		public ReactiveCommand<Unit, Unit> CopyLabel { get; }
		public ReactiveCommand<Unit, Unit> ToggleQrCode { get; }
		public ReactiveCommand<Unit, Unit> ChangeLabelCommand { get; }
		public ReactiveCommand<Unit, Unit> DisplayAddressOnHwCommand { get; }
		public ReactiveCommand<Unit, Unit> GenerateCommand { get; }
		public ReactiveCommand<Unit, Unit> SaveQRCodeCommand { get; }

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			Global = Locator.Current.GetService<Global>();

			_labelSuggestion = new SuggestLabelViewModel(Global);
			_addresses = new ObservableCollection<AddressViewModel>();
			_labelSuggestion.Label = "";

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
				{
					var label = new SmartLabel(_labelSuggestion.Label);
					_labelSuggestion.Label = label;
					if (label.IsEmpty)
					{
						NotificationHelpers.Warning("Label is required.");

						return;
					}

					Dispatcher.UIThread.PostLogException(() =>
						{
							HdPubKey newKey = Global.WalletService.GetReceiveKey(_labelSuggestion.Label, Addresses.Select(x => x.Model).Take(7)); // Never touch the first 7 keys.

							AddressViewModel found = Addresses.FirstOrDefault(x => x.Model == newKey);
							if (found != default)
							{
								Addresses.Remove(found);
							}

							var newAddress = new AddressViewModel(newKey, Global);

							Addresses.Insert(0, newAddress);

							SelectedAddress = newAddress;

							_labelSuggestion.Label = "";
						});
				});

			this.WhenAnyValue(x => x.SelectedAddress).Subscribe(async address =>
				{
					if (Global.UiConfig?.Autocopy is false || address is null)
					{
						return;
					}

					await address.TryCopyToClipboardAsync();
				});

			var isCoinListItemSelected = this.WhenAnyValue(x => x.SelectedAddress).Select(coin => coin is { });

			CopyAddress = ReactiveCommand.CreateFromTask(async () =>
			{
				if (SelectedAddress is null)
				{
					return;
				}

				await SelectedAddress.TryCopyToClipboardAsync();
			},
			isCoinListItemSelected);

			CopyLabel = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(SelectedAddress.Label ?? string.Empty), isCoinListItemSelected);

			ToggleQrCode = ReactiveCommand.Create(() => ToggleSelectedAddress(), isCoinListItemSelected);

#pragma warning disable IDE0053 // Use expression body for lambda expressions
			ChangeLabelCommand = ReactiveCommand.Create(() => { SelectedAddress.InEditMode = true; });
#pragma warning restore IDE0053 // Use expression body for lambda expressions

			DisplayAddressOnHwCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var client = new HwiClient(Global.Network);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				try
				{
					await client.DisplayAddressAsync(KeyManager.MasterFingerprint.Value, SelectedAddress.Model.FullKeyPath, cts.Token);
				}
				catch (HwiException)
				{
					await PinPadViewModel.UnlockAsync(Global);
					await client.DisplayAddressAsync(KeyManager.MasterFingerprint.Value, SelectedAddress.Model.FullKeyPath, cts.Token);
				}
			});

			SaveQRCodeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (SelectedAddress is { })
				{
					await SelectedAddress.SaveQRCodeAsync();
				}
			});

			Observable
				.Merge(DisplayAddressOnHwCommand.ThrownExceptions)
				.Merge(ChangeLabelCommand.ThrownExceptions)
				.Merge(ToggleQrCode.ThrownExceptions)
				.Merge(CopyAddress.ThrownExceptions)
				.Merge(CopyLabel.ThrownExceptions)
				.Merge(GenerateCommand.ThrownExceptions)
				.Merge(SaveQRCodeCommand.ThrownExceptions)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(ex => NotificationHelpers.Error(ex.ToTypeMessageString()));
		}

		public SuggestLabelViewModel LabelSuggestion => _labelSuggestion;

		private void ToggleSelectedAddress()
		{
			SelectedAddress.IsExpanded = !SelectedAddress.IsExpanded;
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Observable
				.FromEventPattern(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.CoinReceived))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => InitializeAddresses())
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
			try
			{
				_addresses?.Clear();
				var walletService = Global.WalletService;

				if (walletService is null)
				{
					return;
				}

				IEnumerable<HdPubKey> keys = walletService.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Reverse();
				foreach (HdPubKey key in keys)
				{
					_addresses.Add(new AddressViewModel(key, Global));
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
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
	}
}
