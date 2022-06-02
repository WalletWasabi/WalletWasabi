using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Tests.Helpers;

public static class CryptoHelpers
{
	public static readonly Scalar ScalarLargestOverflow = new(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
	public static readonly Scalar ScalarN = EC.N;
	public static readonly Scalar ScalarEcnPlusOne = EC.N + Scalar.One;
	public static readonly Scalar ScalarEcnMinusOne = EC.N + Scalar.One.Negate(); // Largest non-overflown scalar.
	public static readonly Scalar ScalarLarge = new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
	public static readonly Scalar ScalarZero = Scalar.Zero;
	public static readonly Scalar ScalarOne = Scalar.One;
	public static readonly Scalar ScalarTwo = new(2);
	public static readonly Scalar ScalarThree = new(3);
	public static readonly Scalar ScalarEcnc = EC.NC;

	public static IEnumerable<Scalar> GetScalars(Func<Scalar, bool> predicate)
	{
		var scalars = new List<Scalar>
			{
				ScalarLargestOverflow,
				ScalarN,
				ScalarEcnPlusOne,
				ScalarEcnMinusOne,
				ScalarLarge,
				ScalarZero,
				ScalarOne,
				ScalarTwo,
				ScalarThree,
				ScalarEcnc
			};

		return scalars.Where(predicate);
	}

	public static int RandomInt(int minInclusive, int maxInclusive)
		=> Random.Shared.Next(minInclusive, maxInclusive + 1);
}
