using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
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
			IWasabiRandom unsecureRandom = new UnsecureRandom();
			using var secureRandomToDispose = new SecureRandom();
			IWasabiRandom secureRandom = secureRandomToDispose;
			for (int i = 0; i < count; i++)
			{
				pseudoSet.Add(unsecureRandom.GetBytes(10));
				secureSet.Add(secureRandom.GetBytes(10));
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
				new UnsecureRandom(),
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

		[Fact]
		public void ScalarTests()
		{
			// Make sure first that scalar equality works within hashset and that the underlying API won't pull the floor out.
			var singleSet = new HashSet<Scalar>();
			var scalar = new Scalar(5);
			var same = new Scalar(5);
			singleSet.Add(scalar);
			singleSet.Add(same);
			Assert.Single(singleSet);

			var randoms = new List<IWasabiRandom>
			{
				new SecureRandom(),
				new UnsecureRandom()
			};

			foreach (var random in randoms)
			{
				// It's probabilistically ensured that it never produces the same scalar, so unit test should pass always.
				var nonZeroSet = new HashSet<Scalar>();
				var count = 100;
				for (int i = 0; i < count; i++)
				{
					Scalar randomScalar = random.GetScalarNonZero();
					nonZeroSet.Add(randomScalar);
				}
				Assert.Equal(count, nonZeroSet.Count);

				// Well, this is unlikely to catch any issues, but it catches the large ones at least.
				Assert.True(nonZeroSet.All(x => x != Scalar.Zero));

				if (random is SecureRandom secureRandom)
				{
					secureRandom.Dispose();
				}
			}
		}

		[Fact]
		public void ScalarInternalTests()
		{
			var mockRandom = new MockRandom();
			IWasabiRandom iWasabiRandom = mockRandom;

			// The random should not be zero.
			mockRandom.GetBytesResults.Add(Scalar.Zero.ToBytes());

			// The random should not overfow.
			mockRandom.GetBytesResults.Add(EC.N.ToBytes());

			// ToDo: EC.N + new Scalar(1) will be Scalar(1) and the overflow will not be set, so it'd be valid. Investigate if it's good like this.

			// The random should not overfow.
			mockRandom.GetBytesResults.Add(new Scalar(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue).ToBytes());

			var one = new Scalar(1);
			mockRandom.GetBytesResults.Add(one.ToBytes());
			var two = new Scalar(2);
			mockRandom.GetBytesResults.Add(two.ToBytes());
			var big = new Scalar(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
			mockRandom.GetBytesResults.Add(big.ToBytes());
			var biggest = EC.N + one.Negate();
			mockRandom.GetBytesResults.Add(biggest.ToBytes());

			var randomScalar = iWasabiRandom.GetScalarNonZero();
			Assert.Equal(one, randomScalar);
			randomScalar = iWasabiRandom.GetScalarNonZero();
			Assert.Equal(two, randomScalar);
			randomScalar = iWasabiRandom.GetScalarNonZero();
			Assert.Equal(big, randomScalar);
			randomScalar = iWasabiRandom.GetScalarNonZero();
			Assert.Equal(biggest, randomScalar);
		}
	}
}
