using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class SyntheticSecretNonceProvider
	{
		private readonly Strobe128 _strobe;

		public SyntheticSecretNonceProvider(Strobe128 strobe, IEnumerable<Scalar> secrets, WasabiRandom random)
		{
			Guard.NotNullOrEmpty(nameof(secrets), secrets);

			_strobe = strobe;

			// add secret inputs as key material
			foreach (var secret in secrets)
			{
				_strobe.Key(secret.ToBytes(), false);
			}

			// add randomness as key material
			_strobe.Key(random.GetBytes(32), false);

			// Set up a generator of vectors of scalars the size as secrets vector
			Sequence = VectorGenerator(secrets.Count());
		}

		public IEnumerable<ScalarVector> Sequence { get; }

		public Scalar GetScalar()
		{
			return new Scalar(_strobe.Prf(32, false));
		}

		private IEnumerable<Scalar> ScalarGenerator()
		{
			while (true)
			{
				yield return GetScalar();
			}
		}

		private IEnumerable<ScalarVector> VectorGenerator(int len)
		{
			while (true)
			{
				// ScalarVector's constructor will call ToArray internally, hopefully
				// minimizing copying
				yield return new ScalarVector(ScalarGenerator().Take(len));
			}
		}
	}
}
