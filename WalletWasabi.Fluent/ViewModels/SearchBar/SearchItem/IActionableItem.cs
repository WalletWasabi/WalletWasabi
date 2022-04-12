using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItem;

public interface IActionableItem : ISearchItem
{
	Func<Task> OnExecution { get; }
}