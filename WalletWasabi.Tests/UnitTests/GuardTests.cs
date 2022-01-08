using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class GuardTests
{
	[Fact]
	public void True()
	{
		Guard.True("foo", true);
		Assert.Throws<ArgumentNullException>(() => Guard.True(null!, true));
		Assert.Throws<ArgumentException>(() => Guard.True("", true));
		Assert.Throws<ArgumentException>(() => Guard.True("  ", true));
		Assert.Throws<ArgumentOutOfRangeException>(() => Guard.True("foo", false));
		Assert.Throws<ArgumentNullException>(() => Guard.True("foo", null));
	}

	[Fact]
	public void False()
	{
		Guard.False("foo", false);
		Assert.Throws<ArgumentNullException>(() => Guard.False(null!, false));
		Assert.Throws<ArgumentException>(() => Guard.False("", false));
		Assert.Throws<ArgumentException>(() => Guard.False("  ", false));
		Assert.Throws<ArgumentOutOfRangeException>(() => Guard.False("foo", true));
		Assert.Throws<ArgumentNullException>(() => Guard.False("foo", null));
	}
}
