using System.Threading.Tasks;
using Moq;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AddressViewModelTests
{
	[Fact]
	public void Hide_command_should_invoke_correct_method()
	{
		var address = Mock.Of<IAddress>(MockBehavior.Loose);
		var context = Mocks.ContextWith(Mocks.DialogThatReturns(true));
		var sut = new AddressViewModel(
			context,
			_ => Task.CompletedTask,
			_ => Task.CompletedTask,
			address);

		sut.HideAddressCommand.Execute(null);

		Mock.Get(address).Verify(x => x.Hide(), Times.Once);
	}



	[Fact]
	public void Properties_are_mapped()
	{
		var testAddress = new TestAddress("ad");
		var labels = new[] { "Label 1", "Label 2" };
		testAddress.SetLabels(labels);
		var sut = new AddressViewModel(Mocks.ContextStub(), _ => Task.CompletedTask, _ => Task.CompletedTask, testAddress);

		Assert.Equal(testAddress.Text, sut.AddressText);
		Assert.Equal(labels, sut.Label);
	}
}
