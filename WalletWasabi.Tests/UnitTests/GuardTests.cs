using System;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class GuardTests
	{
		[Fact]
		public void True()
		{
			_ = Guard.True("foo", true);
			_ = Assert.Throws<ArgumentNullException>(() => Guard.True(null, true));
			_ = Assert.Throws<ArgumentException>(() => Guard.True("", true));
			_ = Assert.Throws<ArgumentException>(() => Guard.True("  ", true));
			_ = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.True("foo", false));
			_ = Assert.Throws<ArgumentNullException>(() => Guard.True("foo", null));
		}

		[Fact]
		public void False()
		{
			_ = Guard.False("foo", false);
			_ = Assert.Throws<ArgumentNullException>(() => Guard.False(null, false));
			_ = Assert.Throws<ArgumentException>(() => Guard.False("", false));
			_ = Assert.Throws<ArgumentException>(() => Guard.False("  ", false));
			_ = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.False("foo", true));
			_ = Assert.Throws<ArgumentNullException>(() => Guard.False("foo", null));
		}
	}
}
