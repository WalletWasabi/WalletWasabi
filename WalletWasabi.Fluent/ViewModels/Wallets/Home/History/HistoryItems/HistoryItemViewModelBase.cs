using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public enum HistoryItemType
{
	Unknown,
	IncomingTransaction,
	OutgoingTransaction,
	SelfTransferTransaction,
	Coinjoin,
	CoinjoinGroup,
	Cancellation,
	CPFP
}

public enum HistoryItemStatus
{
	Unknown,
	Confirmed,
	Pending,
	SpeedUp,
}

public abstract partial class HistoryItemViewModelBase : ViewModelBase
{
	[AutoNotify] private bool _isFlashing;
	[AutoNotify] private int _orderIndex;
	[AutoNotify] private DateTimeOffset _date;
	[AutoNotify] private string _dateString = "";
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private bool _isExpanded;
	[AutoNotify] private string _confirmedToolTip;
	[AutoNotify] private HistoryItemType _itemType;
	[AutoNotify] private HistoryItemStatus _itemStatus;
	private ObservableCollection<HistoryItemViewModelBase>? _children;

	protected HistoryItemViewModelBase(int orderIndex, TransactionSummary transactionSummary)
	{
		OrderIndex = orderIndex;
		TransactionSummary = transactionSummary;
		Id = transactionSummary.GetHash();

		_confirmedToolTip = GetConfirmedToolTip(transactionSummary.GetConfirmations());

		_isConfirmed = transactionSummary.IsConfirmed();

		ClipboardCopyCommand = ReactiveCommand.CreateFromTask<string>(CopyToClipboardAsync);

		this.WhenAnyValue(x => x.IsFlashing)
			.Where(x => x)
			.SubscribeAsync(async _ =>
			{
				await Task.Delay(1260);
				IsFlashing = false;
			});

		IsCancellation = false;
		IsSpeedUp = false;
	}

	protected HistoryItemType GetItemType()
	{
		if (!IsCPFP && IncomingAmount is { } incomingAmount && incomingAmount > Money.Zero && !IsCoinJoin)
		{
			return HistoryItemType.IncomingTransaction;
		}

		if (!IsCPFP && OutgoingAmount is { } outgoingAmount && outgoingAmount > Money.Zero && !IsCoinJoin)
		{
			return HistoryItemType.OutgoingTransaction;
		}

		if (OutgoingAmount == Money.Zero)
		{
			return HistoryItemType.SelfTransferTransaction;
		}

		if (IsCoinJoin && !IsCoinJoinGroup)
		{
			return HistoryItemType.Coinjoin;
		}

		if (IsCoinJoin && IsCoinJoinGroup)
		{
			return HistoryItemType.CoinjoinGroup;
		}

		if (IsCancellation)
		{
			return HistoryItemType.Cancellation;
		}

		if (IsCPFP)
		{
			return HistoryItemType.CPFP;
		}

		return HistoryItemType.Unknown;
	}

	protected HistoryItemStatus GetItemStatus()
	{
		if (IsConfirmed)
		{
			return HistoryItemStatus.Confirmed;
		}

		if (!IsConfirmed && !IsSpeedUp)
		{
			return HistoryItemStatus.Pending;
		}

		if (!IsConfirmed && (IsSpeedUp || IsCPFPd))
		{
			return HistoryItemStatus.SpeedUp;
		}

		return HistoryItemStatus.Unknown;
	}

	protected string GetConfirmedToolTip(int confirmations)
	{
		return $"Confirmed ({confirmations} confirmation{TextHelpers.AddSIfPlural(confirmations)})";
	}

	public uint256 Id { get; }

	public LabelsArray Labels { get; init; }

	public bool IsCoinJoin { get; protected set; }

	public bool IsCoinJoinGroup { get; protected set; }

	public IReadOnlyList<HistoryItemViewModelBase> Children => _children ??= LoadChildren();

	public Money? Balance { get; protected set; }

	public Money? OutgoingAmount { get; protected set; }

	public Money? IncomingAmount { get; protected set; }

	public ICommand? ShowDetailsCommand { get; protected set; }

	public bool IsChild { get; set; }

	public ICommand? ClipboardCopyCommand { get; protected set; }

	public ICommand? SpeedUpTransactionCommand { get; protected set; }

	public ICommand? CancelTransactionCommand { get; protected set; }

	public bool IsCancellation { get; set; }

	public bool IsSpeedUp { get; set; }

	public bool IsCPFP { get; set; }

	public bool IsCPFPd { get; set; }

	public TransactionSummary TransactionSummary { get; }

	private async Task CopyToClipboardAsync(string text)
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			await clipboard.SetTextAsync(text);
		}
	}

	protected virtual ObservableCollection<HistoryItemViewModelBase> LoadChildren()
	{
		throw new NotSupportedException();
	}

	protected void SetAmount(Money amount, Money? fee)
	{
		if (amount < Money.Zero)
		{
			OutgoingAmount = -amount - (fee ?? Money.Zero);
		}
		else
		{
			IncomingAmount = amount;
		}
	}

	public virtual bool HasChildren() => false;

	public static Comparison<HistoryItemViewModelBase?> SortAscending<T>(Func<HistoryItemViewModelBase, T> selector, IComparer<T>? comparer = null)
	{
		return Sort(selector, comparer, reverse: false);
	}

	public static Comparison<HistoryItemViewModelBase?> SortDescending<T>(Func<HistoryItemViewModelBase, T> selector, IComparer<T>? comparer = null)
	{
		return Sort(selector, comparer, reverse: true);
	}

	private static Comparison<HistoryItemViewModelBase?> Sort<T>(Func<HistoryItemViewModelBase, T> selector, IComparer<T>? comparer, bool reverse)
	{
		return (x, y) =>
		{
			var ordering = reverse ? -1 : 1;

			if (x is null && y is null)
			{
				return 0;
			}

			if (x is null)
			{
				return -ordering;
			}

			if (y is null)
			{
				return ordering;
			}

			// Confirmation comparison must be the same for both sort directions..
			var result = x.IsConfirmed.CompareTo(y.IsConfirmed);
			if (result == 0)
			{
				var xValue = selector(x);
				var yValue = selector(y);

				result =
					comparer?.Compare(xValue, yValue) ??
					Comparer<T>.Default.Compare(xValue, yValue);
				result *= ordering;
			}

			return result;
		};
	}
}
