using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.TreeDataGrid;
using ScriptType = WalletWasabi.Fluent.Models.Wallets.ScriptType;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public abstract partial class CoinListItem : ViewModelBase, ITreeDataGridExpanderItem, IDisposable
{
	protected readonly CompositeDisposable _disposables = new();

	private bool? _isSelected;

	[AutoNotify] private bool _isParentSelected;
	[AutoNotify] private bool _isParentPointerOver;
	[AutoNotify] private bool _isControlSelected;

	[AutoNotify] private bool _isControlPointerOver;
	[AutoNotify] private bool _isExpanded;
	[AutoNotify] private bool _isCoinjoining;
	[AutoNotify] private bool _isExcludedFromCoinJoin;
	[AutoNotify] private bool _canBeSelected;

	protected CoinListItem()
	{
		// Temporarily enable the selection no matter what.
		// Should be again restricted once https://github.com/WalletWasabi/WalletWasabi/issues/9972 is implemented.
		// CanBeSelected = !IsCoinjoining;
		CanBeSelected = true;

		ClipboardCopyCommand = ReactiveCommand.CreateFromTask<string>(text => UiContext.Clipboard.SetTextAsync(text));

		this.WhenAnyValue(x => x.IsControlPointerOver)
			.Do(x =>
			{
				foreach (var child in Children)
				{
					child.IsParentPointerOver = x;
				}
			})
			.Subscribe();

		this.WhenAnyValue(x => x.IsControlSelected)
			.Do(x =>
			{
				foreach (var child in Children)
				{
					child.IsParentSelected = x;
				}
			})
			.Subscribe();
	}

	/// <summary>
	/// Proxy property to prevent stack overflow due to internal bug in Avalonia where the OneWayToSource Binding
	/// is replaced by a TwoWay one.when
	/// </summary>
	public bool IsPointerOverProxy
	{
		get => IsControlPointerOver;
		set => IsControlPointerOver = value;
	}

	public bool IsSelectedProxy
	{
		get => IsControlSelected;
		set => IsControlSelected = value;
	}

	public ICommand? ClipboardCopyCommand { get; protected set; }
	public string? BtcAddress { get; protected set; }

	public bool IsPrivate => Labels == CoinPocketHelper.PrivateFundsText;

	public bool IsSemiPrivate => Labels == CoinPocketHelper.SemiPrivateFundsText;

	public bool IsNonPrivate => !IsSemiPrivate && !IsPrivate;

	public IReadOnlyCollection<CoinViewModel> Children { get; protected set; } = new List<CoinViewModel>();

	public bool IsConfirmed { get; protected set; }

	public bool IsBanned { get; protected set; }

	public string ConfirmationStatus { get; protected set; } = "";

	public Amount Amount { get; protected set; } = new(Money.Zero);

	public string? BannedUntilUtcToolTip { get; protected set; }

	public int? AnonymityScore { get; protected set; }

	public LabelsArray Labels { get; protected set; } = LabelsArray.Empty;

	public DateTimeOffset? BannedUntilUtc { get; protected set; }

	public bool IsChild { get; set; }

	public bool IsLastChild { get; set; }

	public bool IgnorePrivacyMode { get; protected set; }

	public bool? IsSelected
	{
		get => _isSelected;
		set => this.RaiseAndSetIfChanged(ref _isSelected, value);
	}

	public ScriptType? ScriptType { get; protected set; }

	public virtual bool HasChildren() => Children.Count != 0;

	public void Dispose() => _disposables.Dispose();

	public abstract string Key { get; }
}
