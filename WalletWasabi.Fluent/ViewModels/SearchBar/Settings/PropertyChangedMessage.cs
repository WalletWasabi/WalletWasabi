namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class PropertyChangedMessage
{
	public string Name { get; }

	public PropertyChangedMessage(string name)
	{
		Name = name;
	}
}