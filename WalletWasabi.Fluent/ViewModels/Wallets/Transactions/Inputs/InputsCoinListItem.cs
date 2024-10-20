using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.TreeDataGrid;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public abstract partial class InputsCoinListItem : ViewModelBase, ITreeDataGridExpanderItem, IDisposable
{
	protected readonly CompositeDisposable _disposables = new();

	[AutoNotify] private bool _isParentPointerOver;
	[AutoNotify] private bool _isControlPointerOver;
	[AutoNotify] private bool _isExpanded;

	protected InputsCoinListItem()
	{
		this.WhenAnyValue(x => x.IsControlPointerOver)
			.Do(x =>
			{
				foreach (var child in Children)
				{
					child.IsParentPointerOver = x;
				}
			})
			.Subscribe();

		this.WhenAnyValue(x => x.IsExpanded)
			.Do(x =>
			{
				foreach (var child in Children)
				{
					child.IsExpanded = x;
				}
			})
			.Subscribe();
	}

	public Amount Amount { get; protected set; } = new(Money.Zero);
	public int? AnonymityScore { get; protected set; }
	public int? TotalInputs { get; set; }
	public IReadOnlyCollection<InputsCoinViewModel> Children { get; protected set; } = new List<InputsCoinViewModel>();
	public bool HasChildren => Children.Count > 0;

	public bool IsChild { get; set; }
	public bool IsLastChild { get; set; }
	public bool IsParentSelected { get; set; } = false;

	public string TitleText { get; set; }
	public int? NbDiff { get; set; }

	public void Dispose() => _disposables.Dispose();
}
