namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class NestedItemConfiguration<TProperty>
{
	public Func<TProperty, bool> IsVisible { get; }
	public ISearchItem Item { get; }

	public NestedItemConfiguration(Func<TProperty, bool> isVisible, ISearchItem item)
	{
		IsVisible = isVisible;
		Item = item;
	}
}