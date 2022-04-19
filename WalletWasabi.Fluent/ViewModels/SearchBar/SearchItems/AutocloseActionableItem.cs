using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public class AutocloseActionableItem : IActionableItem
{
	private readonly IActionableItem _item;

	public AutocloseActionableItem(IActionableItem item, Action collapseParent)
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
	public bool IsDefault => _item.IsDefault;
}