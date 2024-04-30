using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AddressViewModelTests
{
	[Fact]
	public void AddressPropertiesAreExposedCorrectly()
	{
		var testAddress = new TestAddress("ad");
		var labels = new LabelsArray("Label 1", "Label 2");
		testAddress.SetLabels(labels);
		using var sut = new AddressViewModel(MockUtils.ContextStub(), _ => Task.CompletedTask, address => { }, testAddress);

		Assert.Equal(testAddress.Text, sut.AddressText);
		Assert.Equal(labels, sut.Labels);
	}
}
