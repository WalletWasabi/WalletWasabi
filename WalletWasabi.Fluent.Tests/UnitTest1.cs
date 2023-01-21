using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;

namespace WalletWasabi.Fluent.Tests;

public class UnitTest1
{
	[Fact]
	public async Task Test1()
	{
		var sut = new HardwareWalletViewModel(new TestAddress(), new Tester());
		await sut.ShowOnHwWalletCommand.Execute();
	}
}
