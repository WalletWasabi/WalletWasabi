using System;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>
/// Native receive presentation for an address already validated by the wallet.
/// The same state and view run in production and in isolated headless rendering tests.
/// This layer neither generates wallet keys nor performs financial operations.
/// </summary>
public class MobileReceivePresentation : MobilePaymentRequestState
{
	private readonly CompositeDisposable _commands = new();
	private bool _isRequestMode;

	public MobileReceivePresentation(string address, Func<string, IObservable<bool[,]>> generate,
		Func<string, Task> copy, IScheduler scheduler) : base(address, generate, copy, scheduler)
	{
		Address = address;
		var receive = ReactiveCommand.Create(() => SetMode(false), outputScheduler: scheduler);
		var request = ReactiveCommand.Create(() => SetMode(true), outputScheduler: scheduler);
		var retry = ReactiveCommand.Create(RefreshRequest,
			this.WhenAnyValue(x => x.CanRetryQr), outputScheduler: scheduler);
		SelectAddressCommand = receive;
		SelectRequestCommand = request;
		RetryQrCommand = retry;
		_commands.Add(receive);
		_commands.Add(request);
		_commands.Add(retry);
		PropertyChanged += OnStateChanged;
	}

	public string Address { get; }
	public bool IsRequestMode => _isRequestMode;
	public bool IsAddressMode => !_isRequestMode;
	public string Heading => IsRequestMode ? "Request Bitcoin" : "Ready to receive";
	public string CopyLabel => IsRequestMode ? "Copy request" : "Copy address";
	public bool HasQr => Matrix is not null;
	public bool HasError => Error.Length != 0;
	public bool CanRetryQr => !IsGenerating && Matrix is null && PaymentRequest.Length != 0;
	public string QrStatus => IsGenerating ? "Preparing your QR code…" : "QR code unavailable";
	public ICommand SelectAddressCommand { get; }
	public ICommand SelectRequestCommand { get; }
	public ICommand RetryQrCommand { get; }

	private void SetMode(bool request)
	{
		if (_isRequestMode == request) return;
		_isRequestMode = request;
		// Returning to Receive cancels an in-flight request QR before exposing the
		// address-only mode. A hidden amount must never survive in the copied payload.
		if (!request) Amount = "";
		this.RaisePropertyChanged(nameof(IsRequestMode));
		this.RaisePropertyChanged(nameof(IsAddressMode));
		this.RaisePropertyChanged(nameof(Heading));
		this.RaisePropertyChanged(nameof(CopyLabel));
	}

	private void OnStateChanged(object? sender, PropertyChangedEventArgs change)
	{
		if (change.PropertyName == nameof(Amount) && !string.IsNullOrWhiteSpace(Amount)) SetMode(true);
		if (change.PropertyName is nameof(Matrix) or nameof(Error) or nameof(IsGenerating) or nameof(PaymentRequest))
		{
			this.RaisePropertyChanged(nameof(HasQr));
			this.RaisePropertyChanged(nameof(HasError));
			this.RaisePropertyChanged(nameof(CanRetryQr));
			this.RaisePropertyChanged(nameof(QrStatus));
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			PropertyChanged -= OnStateChanged;
			_commands.Dispose();
		}
		base.Dispose(disposing);
	}
}
