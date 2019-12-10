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

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<AddressViewModel> _addresses;
		private AddressViewModel _selectedAddress;
		private SuggestLabelViewModel _labelSuggestion;
		private ObservableAsPropertyHelper<bool> _isItemSelected;
		private ObservableAsPropertyHelper<bool> _isItemExpanded;
		private ObservableAsPropertyHelper<string> _expandMenuCaption;

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

					AvaloniaThreadingExtensions.PostLogException(Dispatcher.UIThread, () =>
					 {
						 var newKey = KeyManager.GetNextReceiveKey(label, out bool minGapLimitIncreased);
						 if (minGapLimitIncreased)
						 {
							 int minGapLimit = KeyManager.MinGapLimit.Value;
							 int prevMinGapLimit = minGapLimit - 1;
							 NotificationHelpers.Warning($"{nameof(KeyManager.MinGapLimit)} increased from {prevMinGapLimit} to {minGapLimit}.");
						 }

						 var newAddress = new AddressViewModel(newKey, Global);
						 Addresses.Insert(0, newAddress);
						 SelectedAddress = newAddress;
						 _labelSuggestion.Label = "";
					 });
				});

			_isItemSelected = this
				.WhenAnyValue(x => x.SelectedAddress)
				.Select(x => x is { })
				.ToProperty(this, x => x.IsItemSelected, scheduler: RxApp.MainThreadScheduler);

			IObservable<bool> canExecuteContextMenuItem = this.WhenAnyValue(x => x.IsItemSelected);

			this.WhenAnyValue(x => x.SelectedAddress).Subscribe(async address =>
				{
					// Dispose the subscriptions if there were any.
					_isItemExpanded?.Dispose();
					_expandMenuCaption?.Dispose();
					_isItemExpanded = null;
					_expandMenuCaption = null;

					if (address is { })
					{
						_isItemExpanded = address
							.WhenAnyValue(x => x.IsExpanded)
							.ToProperty(this, x => x.IsItemExpanded, scheduler: RxApp.MainThreadScheduler);

						_expandMenuCaption = address
							.WhenAnyValue(x => x.ExpandMenuCaption)
							.ToProperty(this, x => x.ExpandMenuCaption, scheduler: RxApp.MainThreadScheduler);

						Observable
							.Merge(_isItemExpanded.ThrownExceptions)
							.Merge(_expandMenuCaption.ThrownExceptions)
							.Subscribe(OnException);

						// Trigger the update.
						this.RaisePropertyChanged(nameof(IsItemExpanded));
						this.RaisePropertyChanged(nameof(ExpandMenuCaption));
					}

					if (Global.UiConfig?.Autocopy is false || address is null)
					{
						return;
					}

					await address.TryCopyToClipboardAsync();
				});

			CopyAddress = ReactiveCommand.CreateFromTask(async () =>
			{
				var selectedAddress = SelectedAddress;
				if (selectedAddress is null)
				{
					return;
				}

				await selectedAddress.TryCopyToClipboardAsync();
			}, canExecuteContextMenuItem);

			CopyLabel = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(SelectedAddress?.Label ?? string.Empty), canExecuteContextMenuItem);

			ToggleQrCode = ReactiveCommand.Create(() =>
			{
				var selectedAddress = SelectedAddress;
				if (selectedAddress is null)
				{
					return;
				}

				selectedAddress.IsExpanded = !selectedAddress.IsExpanded;
			}, canExecuteContextMenuItem);

			ChangeLabelCommand = ReactiveCommand.Create(() =>
			{
				var selectedAddress = SelectedAddress;
				if (selectedAddress is null)
				{
					return;
				}

				SelectedAddress.InEditMode = true;
			}, canExecuteContextMenuItem);

			DisplayAddressOnHwCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var selectedAddress = SelectedAddress;
				if (selectedAddress is null)
				{
					return;
				}

				var client = new HwiClient(Global.Network);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				try
				{
					await client.DisplayAddressAsync(KeyManager.MasterFingerprint.Value, selectedAddress.Model.FullKeyPath, cts.Token);
				}
				catch (HwiException)
				{
					await PinPadViewModel.UnlockAsync(Global);
					await client.DisplayAddressAsync(KeyManager.MasterFingerprint.Value, selectedAddress.Model.FullKeyPath, cts.Token);
				}
			}, canExecuteContextMenuItem);

			SaveQRCodeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var selectedAddress = SelectedAddress;
				if (selectedAddress is null)
				{
					return;
				}

				await selectedAddress.SaveQRCodeAsync();
			}, canExecuteContextMenuItem);

			Observable
				.Merge(DisplayAddressOnHwCommand.ThrownExceptions)
				.Merge(ChangeLabelCommand.ThrownExceptions)
				.Merge(ToggleQrCode.ThrownExceptions)
				.Merge(CopyAddress.ThrownExceptions)
				.Merge(CopyLabel.ThrownExceptions)
				.Merge(GenerateCommand.ThrownExceptions)
				.Merge(SaveQRCodeCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(OnException);
		}

		public SuggestLabelViewModel LabelSuggestion => _labelSuggestion;

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Observable
				.FromEventPattern(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.WalletRelevantTransactionProcessed))
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

		private void OnException(Exception ex)
		{
			NotificationHelpers.Error(ex.ToTypeMessageString());
			Logger.LogWarning(ex);
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

		public bool IsItemSelected => _isItemSelected?.Value ?? default;
		public bool IsItemExpanded => _isItemExpanded?.Value ?? default;
		public string ExpandMenuCaption => _expandMenuCaption?.Value ?? "Select a receive address";
	}
}
