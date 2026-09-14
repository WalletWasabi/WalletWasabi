using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

public sealed class MobileReceiveRequestViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private string _amount = "";
	private string _paymentRequest;
	private string _error = "";
	private bool[,]? _matrix;
	private bool _disposed;

	public MobileReceiveRequestViewModel(ReceiveAddressViewModel source)
	{
		Source = source;
		_paymentRequest = source.Address;
		// Copy eligibility depends on a valid request, not on a transient clipboard error.
		var canCopy = this.WhenAnyValue(x => x.PaymentRequest).Select(x => !string.IsNullOrEmpty(x));
		var copy = ReactiveCommand.CreateFromTask(async () =>
		{
			await source.UiContext.Clipboard.SetTextAsync(PaymentRequest);
			if (!_disposed) Error = "";
		}, canCopy);
		CopyRequestCommand = copy;
		copy.DisposeWith(_disposables);
		copy.ThrownExceptions.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
		{
			if (!_disposed) Error = "Could not access the clipboard. You can try copying again.";
		}).DisposeWith(_disposables);

		this.WhenAnyValue(x => x.Amount)
			.Select(value => TryCreateRequest(source.Address, value, out var request) ? request : null)
			.Do(request =>
			{
				Error = request is null ? "Enter a positive BTC amount with at most eight decimal places, or leave it empty." : "";
				PaymentRequest = request ?? "";
				Matrix = null;
			})
			.Select(request => request is null
				? Observable.Return<bool[,]?>(null)
				: source.UiContext.QrCodeGenerator.Generate(request)
					.Select(matrix => (bool[,]?)matrix)
					.Catch<bool[,]?, Exception>(_ => Observable.Return<bool[,]?>(null)))
			.Switch()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(matrix =>
			{
				Matrix = matrix;
				if (matrix is null && PaymentRequest.Length > 0) Error = "QR generation failed. You can still copy the address or payment request.";
			})
			.DisposeWith(_disposables);
	}

	public ReceiveAddressViewModel Source { get; }
	public string Amount { get => _amount; set => this.RaiseAndSetIfChanged(ref _amount, value); }
	public string PaymentRequest { get => _paymentRequest; private set => this.RaiseAndSetIfChanged(ref _paymentRequest, value); }
	public string Error { get => _error; private set => this.RaiseAndSetIfChanged(ref _error, value); }
	public bool[,]? Matrix { get => _matrix; private set => this.RaiseAndSetIfChanged(ref _matrix, value); }
	public ICommand CopyRequestCommand { get; }

	public static bool TryCreateRequest(string address, string? input, out string request)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		request = address;
		if (string.IsNullOrWhiteSpace(input)) return true;
		var text = input.Trim();
		var separator = text.IndexOf('.');
		if (separator >= 0 && text.Length - separator - 1 > 8) return false;
		foreach (var c in text) if ((c < '0' || c > '9') && c != '.') return false;
		if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || amount > 21_000_000m) return false;
		request = $"bitcoin:{address}?amount={amount.ToString("0.########", CultureInfo.InvariantCulture)}";
		return true;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_disposables.Dispose();
	}
}
