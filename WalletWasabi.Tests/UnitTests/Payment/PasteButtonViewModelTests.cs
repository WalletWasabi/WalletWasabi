using System.Reactive.Linq;
using WalletWasabi.Fluent.Controls.Payment.ViewModels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Payment;

public class PasteButtonViewModelTests
{
	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, false, false)]
	[InlineData(true, true, true)]
	[InlineData(false, true, false)]
	public void Has_new_content(bool isNewContentAvailable, bool isMainWindowActive, bool hasNewContent)
	{
		using var sut = new PasteButtonViewModel(
			Observable.Empty<string>(),
			Observable.Return(isNewContentAvailable),
			Observable.Return(isMainWindowActive));

		Assert.Equal(new[] { hasNewContent }, sut.HasNewContent.RecordChanges());
	}

	[Fact]
	public void Pasting_should_return_content()
	{
		using var sut = new PasteButtonViewModel(
			Observable.Return("hello world"),
			Observable.Empty<bool>(),
			Observable.Empty<bool>());

		Assert.Equal(new[] { "hello world" }, sut.PasteCommand.Execute().RecordChanges());
	}

	[Fact]
	public void Pasting_empty_should_return_empty_list()
	{
		using var sut = new PasteButtonViewModel(
			Observable.Empty(""),
			Observable.Empty<bool>(),
			Observable.Empty<bool>());

		Assert.Empty(sut.PasteCommand.Execute().RecordChanges());
	}
}
