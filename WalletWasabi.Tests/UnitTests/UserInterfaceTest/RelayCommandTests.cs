using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class RelayCommandTests
{
	[Fact]
	public void RelayCommandCanExecuteTest()
	{
		var c1 = new RelayCommand<bool>(_ => { });
		Assert.True(c1.CanExecute(default));

		IRelayCommand c2 = c1;
		Assert.False(c2.CanExecute(default)); // strange case

		IRelayCommand<bool> c3 = c1;
		Assert.True(c3.CanExecute(default));

		IRelayCommand<bool> c4 = new AsyncRelayCommand<bool>(_ => Task.CompletedTask);
		Assert.True(c4.CanExecute(default));

		IRelayCommand c5 = new AsyncRelayCommand<bool>(_ => Task.CompletedTask);
		Assert.True(c5.CanExecute(default));
	}
}
