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

			// Create a new instance with the same parameters and make sure they're equal.
			var x = new UpdateStatus(backendCompatible, clientUpToDate, legalVersion, backendVersion);
			var y = new UpdateStatus(backendCompatible, clientUpToDate, legalVersion, backendVersion);
			Assert.Equal(x, y);
			Assert.Equal(x.GetHashCode(), y.GetHashCode());

			// Change one parameter at a time and make sure they aren't equal.
			y = new UpdateStatus(true, clientUpToDate, legalVersion, backendVersion);
			Assert.NotEqual(x, y);
			Assert.NotEqual(x.GetHashCode(), y.GetHashCode());
			y = new UpdateStatus(backendCompatible, true, legalVersion, backendVersion);
			Assert.NotEqual(x, y);
			Assert.NotEqual(x.GetHashCode(), y.GetHashCode());
			y = new UpdateStatus(backendCompatible, clientUpToDate, new Version(2, 2), backendVersion);
			Assert.NotEqual(x, y);
			Assert.NotEqual(x.GetHashCode(), y.GetHashCode());
			y = new UpdateStatus(backendCompatible, clientUpToDate, legalVersion, 2);
			Assert.NotEqual(x, y);
			Assert.NotEqual(x.GetHashCode(), y.GetHashCode());

			// Mess around with the versions a bit and make sure they aren't equal.
			y = new UpdateStatus(backendCompatible, clientUpToDate, new Version(1, 1, 1), backendVersion);
			Assert.NotEqual(x, y);
			Assert.NotEqual(x.GetHashCode(), y.GetHashCode());
		}
	}
}
