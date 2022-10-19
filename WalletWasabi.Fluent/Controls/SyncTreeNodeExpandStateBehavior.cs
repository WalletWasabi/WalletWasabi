using System.Reactive.Disposables;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

namespace WalletWasabi.Fluent.Controls;

/// <summary>
/// This behavior is needed due to a bug in TreeDataGrid not syncing the expand state when the model changes.
/// </summary>
public class SyncTreeNodeExpandStateBehavior : AttachedToVisualTreeBehavior<TreeDataGridExpanderCell>
{
	private TreeNode TreeNode { get; set; }

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject?.DataContext is not TreeNode treeNode)
		{
			return;
		}

		TreeNode = treeNode;

		this.WhenAnyValue(x => x.TreeNode.IsExpanded)
			.BindTo(AssociatedObject, x => x.IsExpanded)
			.DisposeWith(disposable);
	}
}
