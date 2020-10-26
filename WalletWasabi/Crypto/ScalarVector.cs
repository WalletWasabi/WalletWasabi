using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class ScalarVector : IEnumerable<Scalar>
	{
		public ScalarVector(IEnumerable<Scalar> scalars)
		{
			Guard.NotNullOrEmpty(nameof(scalars), scalars);
			Scalars = scalars.ToArray();
		}

		public ScalarVector(params Scalar[] scalars)
			: this(scalars as IEnumerable<Scalar>)
		{
		}

		private IEnumerable<Scalar> Scalars { get; }

		public IEnumerator<Scalar> GetEnumerator() =>
			Scalars.GetEnumerator();

		public int Count => Scalars.Count();

		public static GroupElement operator *(ScalarVector scalars, GroupElementVector groupElements)
		{
			Guard.NotNull(nameof(scalars), scalars);
			Guard.NotNull(nameof(groupElements), groupElements);
			Guard.True(nameof(groupElements.Count), groupElements.Count == scalars.Count);

			// TODO https://github.com/ElementsProject/secp256k1-zkp/blob/6f3b0c05c2b561bcba6ae7a276699b86414ed1cc/src/ecmult.h#L35-L46
			return Enumerable.Zip(scalars, groupElements, (s, g) => s * g).Sum();
		}

		public static ScalarVector operator *(Scalar scalar, ScalarVector scalars)
		{
			Guard.NotNull(nameof(scalars), scalars);

			return new ScalarVector(scalars.Select(si => scalar * si));
		}

		public static ScalarVector operator +(ScalarVector scalars1, ScalarVector scalars2)
		{
			Guard.NotNull(nameof(scalars1), scalars1);
			Guard.NotNull(nameof(scalars2), scalars2);
			Guard.True(nameof(scalars1.Count), scalars1.Count == scalars2.Count);

			return new ScalarVector(Enumerable.Zip(scalars1, scalars2, (s1, s2) => s1 + s2));
		}

		IEnumerator IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
