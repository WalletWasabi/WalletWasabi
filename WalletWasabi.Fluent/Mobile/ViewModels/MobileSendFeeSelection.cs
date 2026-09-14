using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

public readonly record struct MobileFeeTargetQuote(int RequestedBlocks, int TargetBlocks, decimal SatoshiPerVbyte);

/// <summary>Native fee cards select a confirmation target, not a fabricated transaction fee.</summary>
public sealed class MobileSendFeeSelection : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _lifetime = new();
	private readonly Action<int> _selectTarget;
	private int _preferredTarget;
	private int? _selectedRequest;
	private bool _disposed;
	private string _status = "Waiting for wallet fee estimates…";

	public MobileSendFeeSelection(int preferredTarget, Action<int> selectTarget,
		IObservable<MobileFeeTargetQuote[]> quotes, IScheduler scheduler)
	{
		_selectTarget = selectTarget ?? throw new ArgumentNullException(nameof(selectTarget));
		ArgumentNullException.ThrowIfNull(quotes);
		ArgumentNullException.ThrowIfNull(scheduler);
		_preferredTarget = preferredTarget;
		Options = new[]
		{
			new MobileFeeTargetOption("Economy", 6, Select),
			new MobileFeeTargetOption("Standard", 3, Select),
			new MobileFeeTargetOption("Priority", 1, Select)
		};
		_lifetime.Add(quotes.ObserveOn(scheduler).Subscribe(Update, _ => Update(Array.Empty<MobileFeeTargetQuote>())));
	}

	public IReadOnlyList<MobileFeeTargetOption> Options { get; }
	public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }

	private void Update(MobileFeeTargetQuote[] quotes)
	{
		if (_disposed) return;
		foreach (var option in Options)
		{
			var matches = quotes.Where(x => x.RequestedBlocks == option.RequestedBlocks && x.TargetBlocks > 0 && x.SatoshiPerVbyte > 0).ToArray();
			option.Update(matches.Length == 1 ? matches[0] : null);
		}

		// A highlighted card is a claim about the target used by transaction review.
		// Never round a saved preference to the nearest card, and never silently save
		// a new target when refreshed estimates clamp an existing card differently.
		var available = Options.Where(x => x.IsAvailable).ToArray();
		var selected = available.FirstOrDefault(x => x.RequestedBlocks == _selectedRequest && x.TargetBlocks == _preferredTarget)
			?? available.Where(x => x.TargetBlocks == _preferredTarget)
				.OrderBy(x => Math.Abs((long)x.RequestedBlocks - _preferredTarget)).FirstOrDefault();
		_selectedRequest = selected?.RequestedBlocks;
		RefreshSelection();
		Status = available.Length == 0
			? "Estimates are unavailable. Review can still offer a custom fee rate."
			: selected is not null
				? "Estimated rates. The final transaction fee is calculated in review."
				: _preferredTarget > 0
					? $"The saved target is {_preferredTarget} blocks. Choose a card to change it, or keep that target for review."
					: "Choose a confirmation target, or set the fee in transaction review.";
	}

	private void Select(MobileFeeTargetOption option)
	{
		if (_disposed || !option.IsAvailable) return;
		try
		{
			// Commit presentation state only after saving the target succeeds.
			_selectTarget(option.TargetBlocks);
			_preferredTarget = option.TargetBlocks;
			_selectedRequest = option.RequestedBlocks;
			RefreshSelection();
			Status = "Confirmation target selected. Review shows the final fee before signing.";
		}
		catch (Exception)
		{
			Status = "Could not save the confirmation target. Choose the fee in transaction review.";
		}
	}

	private void RefreshSelection()
	{
		foreach (var option in Options)
			option.IsSelected = option.IsAvailable && option.TargetBlocks == _preferredTarget && option.RequestedBlocks == _selectedRequest;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_lifetime.Dispose();
		foreach (var option in Options) option.Update(null);
	}
}

public sealed class MobileFeeTargetOption : ReactiveObject, ICommand
{
	private readonly Action<MobileFeeTargetOption> _select;
	private MobileFeeTargetQuote? _quote;
	private bool _isSelected;

	internal MobileFeeTargetOption(string title, int requestedBlocks, Action<MobileFeeTargetOption> select)
	{
		Title = title;
		RequestedBlocks = requestedBlocks;
		_select = select;
	}

	public string Title { get; }
	public int RequestedBlocks { get; }
	public int TargetBlocks => _quote?.TargetBlocks ?? 0;
	public bool IsAvailable => _quote.HasValue;
	public bool IsSelected { get => _isSelected; internal set => this.RaiseAndSetIfChanged(ref _isSelected, value); }
	public string Rate => _quote is { } quote ? quote.SatoshiPerVbyte.ToString("0.###", CultureInfo.InvariantCulture) + " sat/vB" : "Unavailable";
	public string Estimate => _quote is { } quote ? $"~{quote.TargetBlocks * 10L} min" : "Waiting";
	public ICommand SelectCommand => this;
	public event EventHandler? CanExecuteChanged;
	public bool CanExecute(object? parameter) => IsAvailable;
	public void Execute(object? parameter) { if (IsAvailable) _select(this); }

	internal void Update(MobileFeeTargetQuote? quote)
	{
		if (_quote == quote) return;
		_quote = quote;
		this.RaisePropertyChanged(nameof(TargetBlocks));
		this.RaisePropertyChanged(nameof(IsAvailable));
		this.RaisePropertyChanged(nameof(Rate));
		this.RaisePropertyChanged(nameof(Estimate));
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		if (!IsAvailable) IsSelected = false;
	}
}
