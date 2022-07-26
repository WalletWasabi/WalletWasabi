using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class ContentCheckerTests
{
	[Theory]
	[InlineData("Old", "New", true, true)]
	[InlineData("Old", "New", false, false)]
	[InlineData("Old", "Old", true, false)]
	[InlineData("Old", "Old", false, false)]
	public async Task Test(string incoming, string current, bool isValid, bool hasNewContent)
	{
		var sut = new ContentChecker<string>(Observable.Return(incoming), Observable.Return(current), _ => isValid);
		var actual = await sut.HasNewContent.LastAsync();
		actual.Should().Be(hasNewContent);
	}
}
