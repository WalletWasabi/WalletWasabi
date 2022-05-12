using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
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
	private ObservableCollection<HistoryItemViewModelBase>? _children;

	protected HistoryItemViewModelBase(int orderIndex, TransactionSummary transactionSummary)
	{
		OrderIndex = orderIndex;
		Id = transactionSummary.TransactionId;

		this.WhenAnyValue(x => x.IsFlashing)
			.Where(x => x)
			.SubscribeAsync(async _ =>
			{
				await Task.Delay(1260);
				IsFlashing = false;
			});
	}

	public uint256 Id { get; }

	public SmartLabel Label { get; protected set; } = SmartLabel.Empty;

	public bool IsCoinJoin { get; protected set; }

	public IReadOnlyList<HistoryItemViewModelBase> Children => _children ??= LoadChildren();

	public Money? Balance { get; protected set; }

	public Money? OutgoingAmount { get; protected set; }

	public Money? IncomingAmount { get; protected set; }

	public ICommand? ShowDetailsCommand { get; protected set; }

	protected virtual ObservableCollection<HistoryItemViewModelBase> LoadChildren()
	{
		throw new NotSupportedException();
	}

	public static Comparison<HistoryItemViewModelBase?> SortAscending<T>(Func<HistoryItemViewModelBase, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return -1;
			}
			else if (y is null)
			{
				return 1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(x), selector(y));
			}
		};
	}

	public static Comparison<HistoryItemViewModelBase?> SortDescending<T>(Func<HistoryItemViewModelBase, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return 1;
			}
			else if (y is null)
			{
				return -1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(y), selector(x));
			}
		};
	}
}
