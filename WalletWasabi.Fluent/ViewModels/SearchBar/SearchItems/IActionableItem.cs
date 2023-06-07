using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

public interface IActionableItem : ISearchItem
{
	Func<Task> Activate { get; }
}
