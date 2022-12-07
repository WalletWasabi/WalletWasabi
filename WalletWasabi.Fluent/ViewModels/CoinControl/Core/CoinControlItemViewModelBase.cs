using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public abstract class CoinControlItemViewModelBase : ViewModelBase, IHierarchicallySelectable, IDisposable
{
	private bool? _isSelected;
	private readonly CompositeDisposable _disposables = new();

	public CoinControlItemViewModelBase(IEnumerable<IHierarchicallySelectable> children)
	{
		Selectables = children.ToList();
		Children = Selectables.Cast<CoinControlItemViewModelBase>().ToList();
		HierarchicalSelectionHandler = new HierarchicalSelectionHandler(this);
		HierarchicalSelectionHandler.DisposeWith(_disposables);
	}

	public bool IsPrivate => Labels == CoinPocketHelper.PrivateFundsText;

	public bool IsSemiPrivate => Labels == CoinPocketHelper.SemiPrivateFundsText;

	public bool IsNonPrivate => !IsSemiPrivate && !IsPrivate;

	public IEnumerable<CoinControlItemViewModelBase> Children { get; }

	public bool IsConfirmed { get; protected set; }

	public bool IsCoinjoining { get; protected set; }

	public bool IsBanned { get; protected set; }

	public string ConfirmationStatus { get; protected set; } = "";

	public Money Amount { get; protected set; } = Money.Zero;

	public string? BannedUntilUtcToolTip { get; protected set; }

	public int AnonymityScore { get; protected set; }

	public SmartLabel Labels { get; protected set; } = SmartLabel.Empty;

	public DateTimeOffset? BannedUntilUtc { get; protected set; }

	public bool IsExpanded { get; set; } = true;

	public HierarchicalSelectionHandler HierarchicalSelectionHandler { get; }

	public bool CanBeSelected { get; protected set; }

	public IEnumerable<IHierarchicallySelectable> Selectables { get; }

	public bool? IsSelected
	{
		get => _isSelected;
		set
		{
			if (!CanBeSelected && value == true)
			{
				return;
			}

			this.RaiseAndSetIfChanged(ref _isSelected, value);
		}
	}

	public void Dispose()
	{
		_disposables.Dispose();
		foreach (var child in Children)
		{
			child.Dispose();
		}
	}
}
