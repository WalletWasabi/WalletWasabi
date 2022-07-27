using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using WalletWasabi.Fluent.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class ContentCheckerTests
{
	[Theory]
	[InlineData("Old", "New", true, true)]
	[InlineData("Old", "New", false, false)]
	[InlineData("Old", "Old", true, false)]
	[InlineData("Old", "Old", false, false)]
	public async Task Has_new_content(string incoming, string current, bool isValid, bool expected)
	{
		var sut = new ContentChecker<string>(Observable.Return(incoming), Observable.Return(current), _ => isValid);
		var hasNewContent = await sut.HasNewContent.LastAsync();
		hasNewContent.Should().Be(expected);
	}
}
