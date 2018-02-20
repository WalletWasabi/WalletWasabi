using System;
using System.Collections.Generic;
using MagicalCryptoWallet.Backend;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class FastBitArrayTest : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public FastBitArrayTest(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void GetBitsTest()
		{
			// 1 1 1 0 1 0 1 1 - 1 0 1 0 1 0 1 1 - 1 0 1 0 1 1 1 0 - 1 0 1 0 1 1 1 0 
			// 1 0 1 1 1 0 1 0 
			var barr = new FastBitArray();
			barr.Length = 50;
			for (var i = 0; i < 40; i++)
			{
				if (i % 7 == 0)
				{
					barr[i] = true;
					i++;
					barr[i] = true;
				}
				else
				{
					barr[i] = i % 2 == 0;
				}
			}

			// Get bits in the same int.
			Assert.Equal((ulong) 0b111, barr.GetBits(0, 3));
			Assert.Equal((ulong) 0b10111, barr.GetBits(0, 5));
			Assert.Equal((ulong) 0b01010111010, barr.GetBits(3, 11));

			// Get bits in cross int.
			Assert.Equal((ulong) 0b101110101110101, barr.GetBits(24, 16));
		}

		[Fact]
		public void SetRandomBitsTest()
		{
			var barr = new FastBitArray(new byte[0]);
			barr.Length = 150;
			var values = new List<int>();
			var lengths = new List<int>();
			var rnd = new Random();
			var pos = 0;

			for (var i = 0; i < 10; i++)
			{
				var val = rnd.Next();
				var len = rnd.Next(1, 20);
				barr.SetBits(pos, (ulong) val, len);

				values.Add(val);
				lengths.Add(len);
				pos += len;
			}

			pos = 0;
			for (int i = 0; i < 10; i++)
			{
				var len = lengths[i];
				var expectedValue = values[i];
				var value = barr.GetBits(pos, len);
				Assert.Equal(((ulong) expectedValue & value), value);
				pos += len;
			}
		}

		[Fact]
		public void SetBitAndGetBitsTest()
		{
			var barr = new FastBitArray(new byte[0]);
			barr.Length = 150;

			var j = true;
			for (var i = 0; i < 64; i += 2)
			{
				if (j)
				{
					barr.SetBit(i, true);
					barr.SetBit(i + 1, true);
				}
				else
				{
					barr.SetBit(i, false);
					barr.SetBit(i + 1, false);
				}

				j = !j;
			}

			for (var i = 0; i < 16; i++)
			{
				Assert.Equal(0b11UL, barr.GetBits(i * 4, 2));
				Assert.Equal(0b00UL, barr.GetBits((i * 4) + 2, 2));
			}

			for (var i = 0; i < 8; i++)
			{
				Assert.Equal(0b0011UL, barr.GetBits(i * 8, 4));
				Assert.Equal(0b0011UL, barr.GetBits((i * 8) + 4, 2));
			}

			Assert.Equal(0b11001UL, barr.GetBits(29, 5));
		}

		[Fact]
		public void SetBitsBigEndianTest()
		{
			var barr = new FastBitArray(new byte[0]);
			barr.Length = 5;
			barr.SetBits(0, 14, 4);
			var val = barr.GetBits(0, 4);

			barr = new FastBitArray(new byte[0]);
			barr.Length = 5;
			barr.SetBit(0, false);
			barr.SetBit(1, true);
			barr.SetBit(2, true);
			barr.SetBit(3, true);
			val = barr.GetBits(0, 4);
		}

		[Fact]
		public void ReconstructionTest()
		{
			var arr = new byte[] {1, 2, 3, 4};
			var b = new FastBitArray(arr);
			var arr2 = b.ToByteArray();
			Assert.Equal(arr.Length, arr2.Length);
			Assert.Equal(arr[0], arr2[0]);
			Assert.Equal(arr[arr.Length-1], arr2[arr2.Length-1]);
			Assert.Equal(4 * 8, b.Length);

			b.Length++;
			Assert.Equal((4 * 8) + 1, b.Length);
			arr2 = b.ToByteArray();
			Assert.Equal(arr[0], arr2[0]);
			Assert.Equal(0, arr2[arr2.Length - 1]);
			b.SetBit(4*8, true);
			arr2 = b.ToByteArray();
			Assert.Equal(arr[0], arr2[0]);
			Assert.Equal(1, arr2[arr2.Length - 1]);

			var b2 = new FastBitArray(arr2);
			var arr3 = b2.ToByteArray();
			Assert.Equal(arr2.Length, arr3.Length);
			Assert.Equal(arr2[0], arr3[0]);
			Assert.Equal(arr2[arr2.Length - 1], arr3[arr3.Length - 1]);
		}
	}
}
