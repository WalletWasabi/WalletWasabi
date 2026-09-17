using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Services;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>Shares only the current validated request after explicit activation; UI-owned updates invalidate stale payloads synchronously.</summary>
public sealed class MobileShareRequestState : ReactiveObject, IDisposable
{
	private readonly IMobileShareService? _service;
	private readonly IScheduler _scheduler;
	private readonly CompositeDisposable _lifetime = new();
	private readonly CancellationTokenSource _cancellation = new();
	private string _payload = "";
	private string _error = "";
	private long _revision;
	private bool _disposed;

	public MobileShareRequestState(IMobileShareService? service, IObservable<string> requests, IScheduler scheduler)
	{
		_service = service;
		_scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
		ArgumentNullException.ThrowIfNull(requests);
		var command = ReactiveCommand.CreateFromTask(PresentAsync,
			this.WhenAnyValue(x => x.Payload).Select(text => IsSupported && text.Length > 0), outputScheduler: scheduler);
		Command = command;
		_lifetime.Add(command);
		_lifetime.Add(requests.Subscribe(text =>
		{
			if (_disposed) return;
			++_revision;
			Payload = text ?? "";
			Error = "";
		}));
	}

	public bool IsSupported => _service is not null;
	public string Payload { get => _payload; private set => this.RaiseAndSetIfChanged(ref _payload, value); }
	public string Error
	{
		get => _error;
		private set { this.RaiseAndSetIfChanged(ref _error, value); this.RaisePropertyChanged(nameof(HasError)); }
	}
	public bool HasError => Error.Length != 0;
	public ICommand Command { get; }

	private async Task PresentAsync()
	{
		if (_disposed || _service is null || Payload.Length == 0) return;
		var revision = _revision;
		var payload = Payload;
		var error = "";
		try { await _service.PresentAsync(payload, _cancellation.Token).ConfigureAwait(false); }
		catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
		catch (Exception) { error = "Could not open the share sheet. You can copy the request instead."; }
		_lifetime.Add(_scheduler.Schedule(() =>
		{
			if (!_disposed && revision == _revision) Error = error;
		}));
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_cancellation.Cancel();
		_lifetime.Dispose();
		_cancellation.Dispose();
	}
}
