using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Tests.Gui.TestDoubles;

public class TestAddress : ReactiveObject, IAddress
{
	private bool _isUsed;

	public TestAddress(string address)
	{
		Text = address;
	}

	public string Text { get; }
	public IEnumerable<string> Labels { get; private set; }

	public bool IsUsed
	{
		get => _isUsed;
		set => this.RaiseAndSetIfChanged(ref _isUsed, value);
	}

	public void Hide()
	{
		IsUsed = true;
	}

	public void SetLabels(IEnumerable<string> labels)
	{
		Labels = labels;
	}

	public Task ShowOnHwWalletAsync()
	{
		return Task.CompletedTask;
	}
}
