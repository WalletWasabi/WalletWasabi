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

		public ReactiveCommand<Unit, Unit> GenerateCommand { get; }

		public ReceiveTabViewModel(WalletViewModel walletViewModel)
			: base("Receive", walletViewModel)
		{
			Global = Locator.Current.GetService<Global>();

			_labelSuggestion = new SuggestLabelViewModel();
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

						 var newAddress = new AddressViewModel(newKey, KeyManager);
						 Addresses.Insert(0, newAddress);
						 SelectedAddress = newAddress;
						 _labelSuggestion.Label = "";
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
					NotificationHelpers.Error(ex.ToTypeMessageString());
					Logger.LogWarning(ex);
				});
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

		private void InitializeAddresses()
		{
			try
			{
				_addresses?.Clear();

				IEnumerable<HdPubKey> keys = KeyManager.GetKeys(x => !x.Label.IsEmpty && !x.IsInternal && x.KeyState == KeyState.Clean).Reverse();
				foreach (HdPubKey key in keys)
				{
					_addresses.Add(new AddressViewModel(key, KeyManager));
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
