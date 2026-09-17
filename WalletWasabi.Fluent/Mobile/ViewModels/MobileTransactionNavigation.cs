using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>
/// Resolves wallet selection intents against asynchronously arriving native rows.
/// This object never starts a transaction or queries a network; it only opens an existing row.
/// Its inputs and disposal must be used on the owning UI thread.
/// </summary>
public sealed class MobileTransactionNavigation : IDisposable
{
	private readonly TransactionSelection _selection;
	private readonly Action _prepareHistory;
	private readonly Func<uint256, Action?> _resolveOpen;
	private readonly CompositeDisposable _lifetime = new();
	private TransactionSelectionRequest? _prepared;
	private bool _applying;
	private bool _disposed;

	public MobileTransactionNavigation(TransactionSelection selection, Action prepareHistory,
		Func<uint256, Action?> resolveOpen, IObservable<Unit> rowsChanged,
		IObservable<Unit> userNavigation, IScheduler scheduler)
	{
		_selection = selection ?? throw new ArgumentNullException(nameof(selection));
		_prepareHistory = prepareHistory ?? throw new ArgumentNullException(nameof(prepareHistory));
		_resolveOpen = resolveOpen ?? throw new ArgumentNullException(nameof(resolveOpen));
		ArgumentNullException.ThrowIfNull(rowsChanged);
		ArgumentNullException.ThrowIfNull(userNavigation);
		ArgumentNullException.ThrowIfNull(scheduler);

		_lifetime.Add(rowsChanged.ObserveOn(scheduler).Subscribe(_ => TryApply()));
		_lifetime.Add(userNavigation
			.Where(_ => !_applying)
			.Subscribe(_ =>
			{
				// Inputs are UI-owned. Cancel synchronously so a queued row refresh
				// cannot open an old intent after the user has deliberately moved on.
				if (!_disposed && _selection.Pending is { } request) _selection.TryConsume(request);
			}));
		_lifetime.Add(selection.WhenAnyValue(x => x.Pending)
			.ObserveOn(scheduler)
			.Subscribe(Prepare));
	}

	public static MobileTransactionNavigation Attach(MobileWalletViewModel model)
	{
		ArgumentNullException.ThrowIfNull(model);
		var rows = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
			handler => model.Transactions.CollectionChanged += handler,
			handler => model.Transactions.CollectionChanged -= handler).Select(_ => Unit.Default);
		var navigation = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
			handler => model.PropertyChanged += handler,
			handler => model.PropertyChanged -= handler)
			.Where(change => change.EventArgs.PropertyName is nameof(model.Section) or nameof(model.Query) or nameof(model.Filter))
			.Select(_ => Unit.Default);

		return new MobileTransactionNavigation(model.Wallet.History.SelectionRequests,
			() => { model.Query = ""; model.Filter = "All"; model.Navigate("history"); },
			id =>
			{
				var row = model.Transactions.FirstOrDefault(item => item.Model.Id == id);
				return row is not null && row.OpenCommand.CanExecute(null)
					? () => row.OpenCommand.Execute(null)
					: null;
			}, rows, navigation,
			// Always post: row reconciliation and its PropertyChanged notifications
			// must finish before a selection can navigate to a different page.
			new SynchronizationContextScheduler(new AvaloniaSynchronizationContext(DispatcherPriority.Background), alwaysPost: true));
	}

	private void Prepare(TransactionSelectionRequest? request)
	{
		if (_disposed) return;
		if (request is null)
		{
			if (_selection.Pending is null) _prepared = null;
			return;
		}
		if (!ReferenceEquals(_selection.Pending, request)) return;
		_prepared = request;
		_applying = true;
		try { _prepareHistory(); }
		finally { _applying = false; }
		TryApply();
	}

	private void TryApply()
	{
		if (_disposed || _applying || _prepared is not { } request ||
			!ReferenceEquals(_selection.Pending, request)) return;
		var open = _resolveOpen(request.TransactionId);
		if (open is null || !_selection.TryConsume(request)) return;

		// Consume before executing: opening the row can itself raise navigation and
		// collection notifications. A single intent must not execute twice.
		_prepared = null;
		_applying = true;
		try { open(); }
		finally { _applying = false; }
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_lifetime.Dispose();
		_prepared = null;
		// A still-pending wallet-owned intent can be handled by its next attached view.
	}
}
