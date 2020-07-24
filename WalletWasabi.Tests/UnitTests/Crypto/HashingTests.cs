using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class HashingTests
	{
		[Fact]
		public void HashCodeSameForSameByteArrays()
		{
			var array1 = Array.Empty<byte>();
			var array2 = Array.Empty<byte>();
			var hashCode1 = HashHelpers.ComputeHashCode(array1);
			var hashCode2 = HashHelpers.ComputeHashCode(array1);
			var hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);

			array1 = new byte[] { 0 };
			array2 = new byte[] { 0 };
			hashCode1 = HashHelpers.ComputeHashCode(array1);
			hashCode2 = HashHelpers.ComputeHashCode(array1);
			hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);

			array1 = new byte[] { 1 };
			array2 = new byte[] { 1 };
			hashCode1 = HashHelpers.ComputeHashCode(array1);
			hashCode2 = HashHelpers.ComputeHashCode(array1);
			hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);

			array1 = new byte[] { 2 };
			array2 = new byte[] { 2 };
			hashCode1 = HashHelpers.ComputeHashCode(array1);
			hashCode2 = HashHelpers.ComputeHashCode(array1);
			hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);

			array1 = new byte[] { 0, 1 };
			array2 = new byte[] { 0, 1 };
			hashCode1 = HashHelpers.ComputeHashCode(array1);
			hashCode2 = HashHelpers.ComputeHashCode(array1);
			hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);

			array1 = new byte[] { 0, 1, 2 };
			array2 = new byte[] { 0, 1, 2 };
			hashCode1 = HashHelpers.ComputeHashCode(array1);
			hashCode2 = HashHelpers.ComputeHashCode(array1);
			hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);

			array1 = new Scalar(int.MaxValue, int.MaxValue - 1, int.MaxValue - 3, int.MaxValue - 7, int.MaxValue - 11, int.MaxValue - 13, int.MaxValue - 17, int.MaxValue - 21).ToBytes();
			array2 = new Scalar(int.MaxValue, int.MaxValue - 1, int.MaxValue - 3, int.MaxValue - 7, int.MaxValue - 11, int.MaxValue - 13, int.MaxValue - 17, int.MaxValue - 21).ToBytes();
			hashCode1 = HashHelpers.ComputeHashCode(array1);
			hashCode2 = HashHelpers.ComputeHashCode(array1);
			hashCode3 = HashHelpers.ComputeHashCode(array2);
			Assert.Equal(hashCode1, hashCode2);
			Assert.Equal(hashCode1, hashCode3);
		}

		[Fact]
		public void Sha256GroupElement()
		{
			var ge = new GroupElement(EC.G);
			Assert.Throws<ArgumentNullException>(() => ge.Sha256(null));
			Assert.Throws<ArgumentOutOfRangeException>(() => ge.Sha256(GroupElement.Infinity));
			Assert.Throws<ArgumentOutOfRangeException>(() => GroupElement.Infinity.Sha256(ge));

			var sha = ge.Sha256(new GroupElement(EC.G));
			var expectedSha = new Scalar(ByteHelpers.FromHex("FD0A9D2A4767F567283A3041BF4E7CA7616E970097C721AE0F13F9BD75BF65A1"));
			Assert.Equal(expectedSha, sha);
		}
	}
}
