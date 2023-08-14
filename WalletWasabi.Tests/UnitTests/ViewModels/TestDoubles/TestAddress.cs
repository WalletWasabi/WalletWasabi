using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

public class TestAddress : ReactiveObject, IAddress
{
	private bool _isUsed;

	public TestAddress(string address)
	{
		Text = address;
	}

	public string Text { get; }
	public LabelsArray Labels { get; private set; }

	public bool IsUsed
	{
		get => _isUsed;
		set => this.RaiseAndSetIfChanged(ref _isUsed, value);
	}

	public void Hide()
	{
		IsUsed = true;
	}

	public void SetLabels(LabelsArray labels)
	{
		Labels = labels;
	}

	public Task ShowOnHwWalletAsync()
	{
		return Task.CompletedTask;
	}
}
