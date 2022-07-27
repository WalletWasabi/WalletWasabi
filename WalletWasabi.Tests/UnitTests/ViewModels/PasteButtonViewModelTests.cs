using System.Reactive.Linq;
using FluentAssertions;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

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

		sut.HasNewContent
			.RecordChanges(() => { })
			.Should()
			.BeEquivalentTo(new[] { hasNewContent });
	}

	[Fact]
	public void Pasting_should_return_content()
	{
		using var sut = new PasteButtonViewModel(
			Observable.Return("hello world"),
			Observable.Empty<bool>(),
			Observable.Empty<bool>());

		sut.PasteCommand
			.Execute()
			.RecordChanges()
			.Should()
			.BeEquivalentTo("hello world");
	}

	[Fact]
	public void Pasting_empty_should_return_emtpy_list()
	{
		using var sut = new PasteButtonViewModel(
			Observable.Empty(""),
			Observable.Empty<bool>(),
			Observable.Empty<bool>());

		sut.PasteCommand
			.Execute()
			.RecordChanges()
			.Should()
			.BeEmpty();
	}
}
