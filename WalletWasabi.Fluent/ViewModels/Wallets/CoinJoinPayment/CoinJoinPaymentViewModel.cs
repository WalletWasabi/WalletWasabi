using NBitcoin;
using ReactiveUI;
using WalletWasabi.WabiSabi.Client.Batching;

namespace WalletWasabi.Fluent.ViewModels.Wallets.CoinJoinPayment;

/// <summary>
/// ViewModel wrapper for a single Payment in the CoinJoin batched payments list.
/// </summary>
public partial class CoinJoinPaymentViewModel : ViewModelBase
{
	private readonly Payment _payment;
	private readonly Network _network;

	[AutoNotify] private string _status;

	public CoinJoinPaymentViewModel(UiContext uiContext, Payment payment, Network network) : base(uiContext)
	{
		_payment = payment;
		_network = network;
		_status = GetStatusText();
	}

	public Guid Id => _payment.Id;

	public string Address => _payment.Destination.ScriptPubKey.GetDestinationAddress(_network)?.ToString() ?? "Unknown";

	public Money Amount => _payment.Amount;

	public string AmountText => ToBtcText(_payment.Amount);

	public bool IsPending => _payment.State is PendingPayment;

	public bool IsInProgress => _payment.State is InProgressPayment or SignedUnknownPayment;

	public bool IsFinished => _payment.State is FinishedPayment;

	public bool IsRejected => _payment.State is RejectedPayment;

	public bool IsCancellable => IsPending;

	public string? TransactionId => _payment.State is FinishedPayment finished
		? finished.TransactionId.ToString()
		: null;

	public string? RejectionReason => _payment.State is RejectedPayment rejected
		? $"Below the coordinator's minimum of {ToBtcText(rejected.MinAllowedOutputAmount)}"
		: null;

	public Payment Payment => _payment;

	private static string ToBtcText(Money amount) => $"{amount.ToDecimal(MoneyUnit.BTC):N8} BTC";

	private string GetStatusText()
	{
		return _payment.State switch
		{
			PendingPayment => "Pending",
			InProgressPayment or SignedUnknownPayment => "In Progress",
			FinishedPayment => "Completed",
			RejectedPayment => "Rejected",
			_ => "Unknown"
		};
	}

	public void UpdateStatus()
	{
		Status = GetStatusText();
		this.RaisePropertyChanged(nameof(IsPending));
		this.RaisePropertyChanged(nameof(IsInProgress));
		this.RaisePropertyChanged(nameof(IsFinished));
		this.RaisePropertyChanged(nameof(IsCancellable));
		this.RaisePropertyChanged(nameof(TransactionId));
	}
}
