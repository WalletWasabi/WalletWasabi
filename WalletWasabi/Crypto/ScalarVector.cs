using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Crypto
{
	public class ScalarVector : IEnumerable<Scalar>
	{
		public ScalarVector(IEnumerable<Scalar> scalars)
		{
			Scalars = scalars.ToArray();
		}

		private IEnumerable<Scalar> Scalars { get; }

		public IEnumerator<Scalar> GetEnumerator() =>
			Scalars.GetEnumerator();

		public int Count => Scalars.Count();

		IEnumerator IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
