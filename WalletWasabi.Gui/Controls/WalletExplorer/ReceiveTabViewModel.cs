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
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private ObservableCollection<AddressViewModel> _addresses;
		private AddressViewModel _selectedAddress;

		private Global Global { get; }

		public ReactiveCommand<Unit, Unit> GenerateCommand { get; }

		public ReceiveTabViewModel(WalletService walletService)
			: base("Receive")
		{
			Global = Locator.Current.GetService<Global>();
			WalletService = walletService;

			LabelSuggestion = new SuggestLabelViewModel(WalletService);
			_addresses = new ObservableCollection<AddressViewModel>();
			LabelSuggestion.Label = "";

			InitializeAddresses();

			GenerateCommand = ReactiveCommand.Create(() =>
				{
					var label = new SmartLabel(LabelSuggestion.Label);
					LabelSuggestion.Label = label;
					if (label.IsEmpty)
					{
						NotificationHelpers.Warning("Observers are required.");
						return;
					}

					AvaloniaThreadingExtensions.PostLogException(Dispatcher.UIThread, () =>
					 {
						 var newKey = WalletService.KeyManager.GetNextReceiveKey(label, out bool minGapLimitIncreased);
						 if (minGapLimitIncreased)
						 {
							 int minGapLimit = WalletService.KeyManager.MinGapLimit.Value;
							 int prevMinGapLimit = minGapLimit - 1;
							 NotificationHelpers.Warning($"{nameof(KeyManager.MinGapLimit)} increased from {prevMinGapLimit} to {minGapLimit}.");
						 }

						 var newAddress = new AddressViewModel(newKey, WalletService.KeyManager);
						 Addresses.Insert(0, newAddress);
						 SelectedAddress = newAddress;
						 LabelSuggestion.Label = "";
					 });
				});

			this.WhenAnyValue(x => x.SelectedAddress)
				.Subscribe(async address =>
				{
					if (Global.UiConfig?.Autocopy is false || address is null)
					{
						return;
					}

					await address.TryCopyToClipboardAsync();
				});

			Observable
				.Merge(GenerateCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});
		}

		private WalletService WalletService { get; }

		public SuggestLabelViewModel LabelSuggestion { get; }

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Observable
				.FromEventPattern(WalletService.TransactionProcessor, nameof(WalletService.TransactionProcessor.WalletRelevantTransactionProcessed))
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

				IEnumerable<HdPubKey> keys = WalletService.KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Reverse();
				foreach (HdPubKey key in keys)
				{
					_addresses.Add(new AddressViewModel(key, WalletService.KeyManager));
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
