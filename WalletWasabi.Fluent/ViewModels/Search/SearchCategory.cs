namespace WalletWasabi.Fluent.ViewModels.Search;

public class SearchCategory
{
	public SearchCategory(string title, int order)
	{
		Title = title;
		Order = order;
	}

	public string Title { get; }

	public int Order { get; }
}
