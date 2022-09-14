namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

internal class Property<T>
{
	public Property(string title, T value)
	{
		Title = title;
		Value = value;
	}

	public string Title { get; }
	public T Value { get; }
}