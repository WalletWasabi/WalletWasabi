using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// This interface serves the purpose of enabling Mocks for unit testing of the ViewModels that consume it.
/// It belongs to the Model part in the Model-View-ViewModel pattern
/// </summary>
public interface IAddress : IReactiveObject
{
	string Text { get; }
	IEnumerable<string> Labels { get; }

	bool IsUsed { get; }

	void Hide();

	void SetLabels(IEnumerable<string> labels);

	Task ShowOnHwWalletAsync();
}
