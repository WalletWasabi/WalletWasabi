using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Models.Wallets;

/// <summary>
/// This interface serves the purpose of enabling Mocks for unit testing of the ViewModels that consume it.
/// It belongs to the Model part in the Model-View-ViewModel pattern
/// </summary>
public interface IAddress : IReactiveObject
{
	string Text { get; }

	LabelsArray Labels { get; }

	void Hide();

	void SetLabels(LabelsArray labels);

	Task ShowOnHwWalletAsync();
}
