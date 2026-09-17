using System;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>
/// Presentation state for an already wallet-validated receiving address. The generator and
/// clipboard are injected; this class never creates keys, opens a network connection, or signs.
/// Amount changes and disposal must originate on the owning UI thread.
/// </summary>
public class MobilePaymentRequestState : ReactiveObject, IDisposable
{
	private readonly string _address;
	private readonly Func<string, IObservable<bool[,]>> _generate;
	private readonly Func<string, Task> _copy;
	private readonly IScheduler _scheduler;
	private readonly CompositeDisposable _lifetime = new();
	private readonly SerialDisposable _generation = new();
	private string _amount = "";
	private string _request = "";
	private string _validationError = "";
	private string _qrError = "";
	private string _clipboardError = "";
	private bool[,]? _matrix;
	private bool _isGenerating;
	private bool _disposed;
	private long _revision;

	public MobilePaymentRequestState(string address, Func<string, IObservable<bool[,]>> generate,
		Func<string, Task> copy, IScheduler scheduler)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		_address = address;
		_generate = generate ?? throw new ArgumentNullException(nameof(generate));
		_copy = copy ?? throw new ArgumentNullException(nameof(copy));
		_scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
		_lifetime.Add(_generation);
		var canCopy = this.WhenAnyValue(x => x.PaymentRequest).Select(x => x.Length != 0);
		var command = ReactiveCommand.CreateFromTask(CopyAsync, canCopy, outputScheduler: scheduler);
		CopyRequestCommand = command;
		_lifetime.Add(command);
		Regenerate();
	}

	public string Amount
	{
		get => _amount;
		set
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (string.Equals(_amount, value, StringComparison.Ordinal)) return;
			this.RaiseAndSetIfChanged(ref _amount, value ?? "");
			Regenerate();
		}
	}

	public string PaymentRequest { get => _request; private set => this.RaiseAndSetIfChanged(ref _request, value); }
	public bool[,]? Matrix { get => _matrix; private set => this.RaiseAndSetIfChanged(ref _matrix, value); }
	public bool IsGenerating { get => _isGenerating; private set => this.RaiseAndSetIfChanged(ref _isGenerating, value); }
	public string Error => _validationError.Length > 0 ? _validationError : _qrError.Length > 0 ? _qrError : _clipboardError;
	public ICommand CopyRequestCommand { get; }

	/// <summary>Retry the current request without changing its validated payload.</summary>
	protected void RefreshRequest()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		Regenerate();
	}

	private void Regenerate()
	{
		var revision = ++_revision;
		_generation.Disposable = Disposable.Empty;
		Matrix = null;
		IsGenerating = false;
		_validationError = _qrError = _clipboardError = "";
		if (!TryCreateRequest(_address, Amount, out var request))
		{
			PaymentRequest = "";
			_validationError = "Enter a positive BTC amount with at most eight decimal places, or leave it empty.";
			this.RaisePropertyChanged(nameof(Error));
			return;
		}
		PaymentRequest = request;
		this.RaisePropertyChanged(nameof(Error));
		IsGenerating = true;

		// Own the subscription before subscribing: generator construction and delivery
		// may be synchronous. Disposal also cancels queued ObserveOn notifications.
		var subscription = new SingleAssignmentDisposable();
		_generation.Disposable = subscription;
		var received = false;
		subscription.Disposable = Observable.Defer(() => _generate(request))
			.Take(1)
			.ObserveOn(_scheduler)
			.Subscribe(matrix =>
			{
				if (!IsCurrent(revision)) return;
				received = true;
				if (matrix is null || matrix.Length == 0 || matrix.GetLength(0) != matrix.GetLength(1))
				{
					FailQr(revision);
					return;
				}
				Matrix = matrix;
				IsGenerating = false;
			}, _ => FailQr(revision), () =>
			{
				if (!received) FailQr(revision);
			});
	}

	private bool IsCurrent(long revision) => !_disposed && revision == _revision;

	private void FailQr(long revision)
	{
		if (!IsCurrent(revision)) return;
		Matrix = null;
		IsGenerating = false;
		_qrError = "QR generation failed. You can still copy the address or payment request.";
		this.RaisePropertyChanged(nameof(Error));
	}

	private async Task CopyAsync()
	{
		var revision = _revision;
		var request = PaymentRequest;
		if (_disposed || request.Length == 0) return;
		var error = "";
		try { await _copy(request).ConfigureAwait(false); }
		catch (Exception) { error = "Could not access the clipboard. You can try copying again."; }
		_lifetime.Add(_scheduler.Schedule(() =>
		{
			if (!IsCurrent(revision)) return;
			_clipboardError = error;
			// Never clear a newer validation/QR failure when an earlier copy completes.
			this.RaisePropertyChanged(nameof(Error));
		}));
	}

	public static bool TryCreateRequest(string address, string? input, out string request)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		request = "";
		if (string.IsNullOrWhiteSpace(input)) { request = address; return true; }
		var text = input.Trim();
		var separator = text.IndexOf('.');
		if (separator >= 0 && text.Length - separator - 1 > 8) return false;
		foreach (var c in text) if ((c < '0' || c > '9') && c != '.') return false;
		if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
			amount <= 0 || amount > 21_000_000m) return false;
		request = $"bitcoin:{address}?amount={amount.ToString("0.########", CultureInfo.InvariantCulture)}";
		return true;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed) return;
		_disposed = true;
		++_revision;
		if (disposing) _lifetime.Dispose();
		_matrix = null;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
