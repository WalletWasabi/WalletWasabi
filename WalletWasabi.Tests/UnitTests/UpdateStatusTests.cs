using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class UpdateStatusTests
	{
		[Fact]
		public void TestEquality()
		{
			var backendCompatible = false;
			var clientUpToDate = false;
			var legalVersion = new Version(1, 1);
			ushort backendVersion = 1;

			var x = new UpdateStatus(backendCompatible, clientUpToDate, legalVersion, backendVersion);
			var y = new UpdateStatus(backendCompatible, clientUpToDate, legalVersion, backendVersion);
			Assert.Equal(x, y);

			y = new UpdateStatus(true, clientUpToDate, legalVersion, backendVersion);
			Assert.NotEqual(x, y);

			y = new UpdateStatus(backendCompatible, true, legalVersion, backendVersion);
			Assert.NotEqual(x, y);

			y = new UpdateStatus(backendCompatible, clientUpToDate, new Version(2, 2), backendVersion);
			Assert.NotEqual(x, y);

			y = new UpdateStatus(backendCompatible, clientUpToDate, legalVersion, 2);
			Assert.NotEqual(x, y);
		}
	}
}
