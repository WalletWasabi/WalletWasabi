namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public static class TreeNodeMixin
{
	public static TOutput? Apply<TInput, TOutput>(this TreeNode node, Func<TInput, TOutput> filter)
	{
		if (node.Value is TInput value)
		{
			return filter(value);
		}

		return default;
	}
}
