using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchItem
{
	public Func<Task> OnExecution { get; }

	public SearchItem(string name, string description, Func<Task> onExecution, string category,
		IEnumerable<string>? keywords = null)
	{
		Name = name;
		Description = description;
		OnExecution = onExecution;
		Category = category;
		Keywords = keywords ?? Enumerable.Empty<string>();
	}

	public string Name { get; }
	public string Description { get; }
	public ComposedKey Key => new(Name);
	public string? Icon { get; set; }
	public string Category { get; }
	public IEnumerable<string> Keywords { get; }
}