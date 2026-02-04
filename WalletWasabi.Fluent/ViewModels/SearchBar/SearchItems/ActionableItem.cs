using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class ActionableItem : IActionableItem
{
	public ActionableItem(string name, string description, Func<Task> onExecution, string category, IEnumerable<string>? keywords = null)
	{
		Name = name;
		Description = description;
		Activate = onExecution;
		Category = category;
		Keywords = keywords ?? Enumerable.Empty<string>();
		Command = ReactiveCommand.CreateFromTask(onExecution);
	}

	public ICommand Command { get; set; }
	public Func<Task> Activate { get; }
	public string Name { get; }
	public string Description { get; }
	public ComposedKey Key => new(Name);
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
	public bool IsDefault { get; set; }
	public int Priority { get; set; }
}
