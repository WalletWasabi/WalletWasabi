using System.Threading.Tasks;
using Moq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AddressViewModelTests
{
	[Fact]
	public void HideCommandShouldInvokeCorrectMethod()
	{
		var address = Mock.Of<IAddress>(MockBehavior.Loose);
		var context = new UiContextBuilder().WithDialogThatReturns(true).Build();
		var sut = new AddressViewModel(
			context,
			_ => Task.CompletedTask,
			_ => { },
			address);

		sut.HideAddressCommand.Execute(null);

		Mock.Get(address).Verify(x => x.Hide(), Times.Once);
	}

	[Fact]
	public void AddressPropertiesAreExposedCorrectly()
	{
		var testAddress = new TestAddress("ad");
		var labels = new LabelsArray("Label 1", "Label 2");
		testAddress.SetLabels(labels);
		var sut = new AddressViewModel(MockUtils.ContextStub(), _ => Task.CompletedTask, _ => { }, testAddress);

		Assert.Equal(testAddress.Text, sut.AddressText);
		Assert.Equal(labels, sut.Labels);
	}
}
