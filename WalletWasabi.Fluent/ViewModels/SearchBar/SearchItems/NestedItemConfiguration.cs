namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class NestedItemConfiguration<TProperty>
{
	public NestedItemConfiguration(Func<TProperty?, bool> isDisplayed, ISearchItem item)
	{
		IsDisplayed = isDisplayed;
		Item = item;
	}

	public Func<TProperty?, bool> IsDisplayed { get; }
	public ISearchItem Item { get; }
}
