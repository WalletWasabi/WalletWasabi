using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Presentation;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>Test-only draft and command spies. No wallet is funded, signed or broadcast.</summary>
internal sealed class MobileSendFixture : ReactiveObject, IMobileSendPresentation, IDisposable
{
	private string _to = Address;
	private decimal? _amount = 0.01m;
	private bool _busy;
	private bool _fixed;
	private bool _conversionReversed;
	public MobileSendFixture()
	{
		FeeSelection = new MobileSendFeeSelection(3, target => SelectedTarget = target,
			Observable.Return(new[] { new MobileFeeTargetQuote(6, 6, 2), new(3, 3, 6), new(1, 1, 15) }), ImmediateScheduler.Instance);
		PasteCommand = new ActionCommand(() => { Invocations.Add("paste"); To = Address; });
		QrCommand = new ActionCommand(() => Invocations.Add("scan"));
		InsertMaxCommand = new ActionCommand(() => { Invocations.Add("max"); AmountBtc = 0.2456m; });
		AddRecipientCommand = new ActionCommand(() => Invocations.Add("add-recipient"));
	}
	public static string Address
	{
		get
		{
			using var key = new NBitcoin.Key(Enumerable.Repeat((byte)0x35, 32).ToArray());
			return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest).ToString();
		}
	}
	public string Caption => "";
	public string To { get => _to; set { this.RaiseAndSetIfChanged(ref _to, value); ChangedErrors(nameof(To)); } }
	public decimal? AmountBtc { get => _amount; set { this.RaiseAndSetIfChanged(ref _amount, value); ChangedErrors(nameof(AmountBtc)); } }
	public decimal ExchangeRate => 68344m;
	public bool ConversionReversed { get => _conversionReversed; set => this.RaiseAndSetIfChanged(ref _conversionReversed, value); }
	public Amount? BalanceLatest { get; } = new(Money.Coins(0.2456m));
	public bool IsFixedAddress => _fixed;
	public bool IsFixedAmount => _fixed;
	public bool IsBusy { get => _busy; set => this.RaiseAndSetIfChanged(ref _busy, value); }
	public bool IsNotInDonationWorkflow => true;
	public bool IsQrButtonVisible => true;
	public bool IsPrimarySubtractFee => false;
	public bool DisplaySilentPaymentInfo => false;
	public bool IsPayJoin => false;
	public bool IsPayToMany => false;
	public string DefaultLabel => "";
	public ObservableCollection<string> Labels { get; } = new() { "Invoice 1042" };
	public ObservableCollection<string> TopSuggestions { get; } = new();
	public ObservableCollection<string> Suggestions { get; } = new();
	public bool IsCurrentTextValid { get; set; } = true;
	public bool ForceAdd { get; set; }
	public IEnumerable AdditionalRecipients => Array.Empty<object>();
	public MobileSendFeeSelection FeeSelection { get; }
	public ICommand PasteCommand { get; }
	public ICommand QrCommand { get; }
	public ICommand InsertMaxCommand { get; }
	public ICommand AddRecipientCommand { get; }
	public List<string> Invocations { get; } = new();
	public int SelectedTarget { get; private set; } = 3;
	public bool HasErrors => GetErrors(nameof(To)).Cast<object>().Any() || GetErrors(nameof(AmountBtc)).Cast<object>().Any();
	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
	public IEnumerable GetErrors(string? propertyName) => propertyName switch
	{
		nameof(To) when !AddressParser.Parse(To, Network.RegTest).IsOk => new[] { "Enter a valid regtest Bitcoin address." },
		nameof(AmountBtc) when AmountBtc is null or <= 0 or > 0.2456m => new[] { "Enter an amount within the available balance." },
		_ => Array.Empty<string>()
	};
	public void LockPayment()
	{
		_fixed = true;
		this.RaisePropertyChanged(nameof(IsFixedAddress));
		this.RaisePropertyChanged(nameof(IsFixedAmount));
	}
	private void ChangedErrors(string property) { ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property)); this.RaisePropertyChanged(nameof(HasErrors)); }
	public void Dispose() => FeeSelection.Dispose();
	private sealed class ActionCommand(Action action) : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }
		public bool CanExecute(object? parameter) => true;
		public void Execute(object? parameter) => action();
	}
}
