namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public interface IContentSearchItem : ISearchItem
{
	object Content { get; }
	public bool IsEnabled { get; }
}
