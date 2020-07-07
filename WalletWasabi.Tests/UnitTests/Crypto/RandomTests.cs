using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class RandomTests
	{
		[Fact]
		public void GetBytesArgumentTests()
		{
			var randoms = new List<IWasabiRandom>
			{
				new SecureRandom(),
				new PseudoRandom(),
				new MockRandom()
			};

			foreach (var random in randoms)
			{
				Assert.Throws<ArgumentOutOfRangeException>(() => random.GetBytes(int.MinValue));
				Assert.Throws<ArgumentOutOfRangeException>(() => random.GetBytes(-1));
				Assert.Throws<ArgumentOutOfRangeException>(() => random.GetBytes(0));

				if (random is MockRandom == false)
				{
					var r1 = random.GetBytes(1);
					Assert.Single(r1);
					var r2 = random.GetBytes(2);
					Assert.Equal(2, r2.Length);
				}

				if (random is SecureRandom secureRandom)
				{
					secureRandom.Dispose();
				}
			}
		}
	}
}
