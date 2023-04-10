using System.Threading.Tasks;
using Avalonia.Input.Platform;
using FluentAssertions;
using Moq;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AddressViewModelTests
{
	// TODO: Fix this
	[Fact]
	public void Hide_command_should_invoke_correct_method()
	{
		//var address = Mock.Of<IAddress>(MockBehavior.Loose);
		//var context = Mocks.ContextWithDialogResult(true);
		//var sut = new AddressViewModel(
		//	_ => Task.CompletedTask,
		//	_ => Task.CompletedTask,
		//	address,
		//	context);

		//sut.HideAddressCommand.Execute(null);

		//Mock.Get(address).Verify(x => x.Hide(), Times.Once);
	}

	private static UiContext GetUiContext()
	{
		return new UiContext(Mock.Of<IQrCodeGenerator>(), Mock.Of<IClipboard>());
	}

	[Fact]
	public void Properties_are_mapped()
	{
		var testAddress = new TestAddress("ad");
		var labels = new[] { "Label 1", "Label 2" };
		testAddress.SetLabels(labels);
		var sut = new AddressViewModel(GetUiContext(), _ => Task.CompletedTask, _ => Task.CompletedTask, testAddress);

		sut.AddressText.Should().Be(testAddress.Text);
		sut.Label.Should().BeEquivalentTo(labels);
	}
}
