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

		private IEnumerable<Scalar> Scalars { get; }

		public IEnumerator<Scalar> GetEnumerator() =>
			Scalars.GetEnumerator();

		public int Count => Scalars.Count();

		public static GroupElement operator *(ScalarVector scalars, GroupElementVector groupElements)
		{
			Guard.NotNull(nameof(scalars), scalars);
			Guard.NotNull(nameof(groupElements), groupElements);
			Guard.True(nameof(groupElements.Count), groupElements.Count == scalars.Count);

			return Enumerable.Zip(scalars, groupElements, (s, G) => s * G).Sum();
		}

		IEnumerator IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
