namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class NestedItemConfiguration<TProperty>
{
	public NestedItemConfiguration(Func<TProperty?, bool> isVisibleSelector, ISearchItem item)
	{
		IsVisibleSelector = isVisibleSelector;
		Item = item;
	}

	public Func<TProperty?, bool> IsVisibleSelector { get; }
	public ISearchItem Item { get; }
}
