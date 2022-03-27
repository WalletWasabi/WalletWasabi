using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class AutocloseActionableItem : IActionableItem
{
	private readonly ActionableItem _item;

	public AutocloseActionableItem(ActionableItem item, Action collapseParent)
	{
		_item = item;
		Command = ReactiveCommand.CreateFromTask(async () =>
		{
			collapseParent();
			await item.OnExecution();
		});
	}

	public ReactiveCommand<Unit, Unit> Command { get; set; }
	public Func<Task> OnExecution => _item.OnExecution;

	public string Name => _item.Name;

	public string Description => _item.Description;

	public ComposedKey Key => _item.Key;

	public string? Icon
	{
		get => _item.Icon;
		set => _item.Icon = value;
	}

	public string Category => _item.Category;

	public IEnumerable<string> Keywords => _item.Keywords;
}

public interface IActionableItem : ISearchItem
{
	Func<Task> OnExecution { get; }
}

public interface ISearchItem
{
	string Name { get; }
	string Description { get; }
	ComposedKey Key { get; }
	string? Icon { get; set; }
	string Category { get; }
	IEnumerable<string> Keywords { get; }
}