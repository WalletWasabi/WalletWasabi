using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

public class TestAddress : ReactiveObject, IAddress
{
	public TestAddress(string address, ScriptType scriptType)
	{
		Text = address;
		ShortenedText = address;
		ScriptType = scriptType;
	}

	public string Text { get; }
	public string ShortenedText { get; }
	public LabelsArray Labels { get; private set; }
	public ScriptType ScriptType { get; }

	public bool IsUsed
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
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
