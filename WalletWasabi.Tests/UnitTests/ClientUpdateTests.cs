using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.ClientUpdates;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class ClientUpdateTests
	{
		public const string ValidTitle = "Foo";
		public const string ValidDescription = "Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567";
		public readonly DateTimeOffset ValidDate = DateTimeOffset.UtcNow;
		public readonly Uri ValidLink = new Uri("https://foo.bar.com");

		[Fact]
		public void UpdateItemConstructor()
		{
			new UpdateItem(ValidDate, ValidTitle, ValidDescription, ValidLink);
		}

		[Theory]
		[InlineData("")]
		[InlineData("F")]
		[InlineData("foo")]
		[InlineData("Foo Foo foo")]
		[InlineData("Foo foo Foo")]
		[InlineData("foo Foo Foo")]
		[InlineData("Foo Foo Foo Foo")]
		[InlineData("Foo Foo  Foo")]
		[InlineData("Foo1234567 Foo1234567 Foo12345678")]
		[InlineData("Foo ")]
		[InlineData(" Foo")]
		[InlineData("Foo\nFoo")]
		[InlineData("Foo\rFoo")]
		[InlineData("Foo\n\rFoo")]
		public void BadTitle(string title)
		{
			Assert.ThrowsAny<FormatException>(() => new UpdateItem(ValidDate, title, ValidDescription, ValidLink));
		}

		[Theory]
		[InlineData("")]
		[InlineData("Bar1234567 Bar1234567 Bar1234567 Bar1234567")]
		[InlineData("bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567")]
		[InlineData("Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567")]
		[InlineData("Bar1234567  Bar1234567 Bar1234567 Bar1234567 Bar1234567")]
		[InlineData("Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567 ")]
		[InlineData(" Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567")]
		[InlineData("Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567\nBar1234567")]
		[InlineData("Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567\rBar1234567")]
		[InlineData("Bar1234567 Bar1234567 Bar1234567 Bar1234567 Bar1234567\n\rBar1234567")]
		public void BadDescription(string description)
		{
			Assert.ThrowsAny<FormatException>(() => new UpdateItem(ValidDate, ValidTitle, description, ValidLink));
		}
	}
}
