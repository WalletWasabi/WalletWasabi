using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchItem
{
	public ICommand Command { get; }

	public SearchItem(string name, string description, ICommand command, string category)
	{
		Name = name;
		Description = description;
		Command = command;
		Category = category;
	}

	public string Name { get; }
	public string Description { get; }
	public ComposedKey Key => new(Name);
	public string Icon { get; set; }
	public string Category { get; }
}