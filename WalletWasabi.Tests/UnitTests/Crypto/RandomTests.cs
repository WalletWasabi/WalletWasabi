using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class RandomTests
	{
		[Fact]
		public void BasicTests()
		{
			// Make sure byte array comparision works within hashset and that the underlying API won't pull the floor out.
			var byteArray = new byte[] { 1, 2, 3 };
			var sameByteArray = new byte[] { 1, 2, 3 };
			var differentByteArray = new byte[] { 4, 5, 6 };
			var twoSet = new HashSet<byte[]>(new ByteArrayEqualityComparer())
			{
				byteArray,
				sameByteArray,
				differentByteArray
			};

			Assert.True(ByteHelpers.CompareFastUnsafe(byteArray, sameByteArray));
			Assert.False(ByteHelpers.CompareFastUnsafe(byteArray, differentByteArray));
			Assert.Equal(2, twoSet.Count);

			// It's probabilistically ensured that it never produces the same scalar, so unit test should pass always.
			var pseudoSet = new HashSet<byte[]>();
			var secureSet = new HashSet<byte[]>();
			var count = 100;
			IWasabiRandom iPseudoRandom = new SecureRandom();
			using var secureRandom = new SecureRandom();
			IWasabiRandom iSecureRandom = new SecureRandom();
			for (int i = 0; i < count; i++)
			{
				pseudoSet.Add(iPseudoRandom.GetBytes(10));
				secureSet.Add(iSecureRandom.GetBytes(10));
			}
			Assert.Equal(count, pseudoSet.Count);
			Assert.Equal(count, secureSet.Count);
		}

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
