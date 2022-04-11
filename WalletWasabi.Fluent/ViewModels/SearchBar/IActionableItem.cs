using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public interface IActionableItem : ISearchItem
{
	Func<Task> OnExecution { get; }
}