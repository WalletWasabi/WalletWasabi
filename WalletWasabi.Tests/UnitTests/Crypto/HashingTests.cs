using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

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
}
