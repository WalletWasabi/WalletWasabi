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

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public abstract partial class HistoryItemViewModelBase : ViewModelBase
{
	[AutoNotify] private bool _isFlashing;
	[AutoNotify] private int _orderIndex;
	[AutoNotify] private DateTimeOffset _date;
	[AutoNotify] private string _dateString = "";
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private bool _isExpanded;
	[AutoNotify] private string _confirmedToolTip;
	private ObservableCollection<HistoryItemViewModelBase>? _children;

	protected HistoryItemViewModelBase(int orderIndex, TransactionSummary transactionSummary)
	{
		OrderIndex = orderIndex;
		Id = transactionSummary.TransactionId;
		_confirmedToolTip = "Confirmed";

		ClipboardCopyCommand = ReactiveCommand.CreateFromTask<string>(CopyToClipboardAsync);

		this.WhenAnyValue(x => x.IsFlashing)
			.Where(x => x)
			.SubscribeAsync(async _ =>
			{
				await Task.Delay(1260);
				IsFlashing = false;
			});
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

	public ICommand? ClipboardCopyCommand { get; protected set; }

	public ICommand? SpeedUpTransactionCommand { get; protected set; }

	public ICommand? CancelTransactionCommand { get; protected set; }

	public TransactionKind TransactionKind
	{
		get
		{
			if (IsCoinJoin && IsCoinJoinGroup)
			{
				return new TransactionKind("Coinjoins", "double_shield_regular");
			}

			if (IsCoinJoin)
			{
				return new TransactionKind("Coinjoin", "shield_regular");
			}

			return new TransactionKind("Transaction", "normal_transaction");
		}
	}

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
