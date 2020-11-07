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
		private readonly Strobe128 Strobe;
		private readonly int SecretCount;

		public SyntheticSecretNonceProvider(Strobe128 strobe, IEnumerable<Scalar> secrets, WasabiRandom random)
		{
			Guard.NotNullOrEmpty(nameof(secrets), secrets);
			Strobe = strobe;
			SecretCount = secrets.Count();

			// add secret inputs as key material
			foreach (var secret in secrets)
			{
				Strobe.Key(secret.ToBytes(), false);
			}

			Strobe.Key(random.GetBytes(32), false);
		}

		private IEnumerable<Scalar> Sequence()
		{
			while (true)
			{
				yield return new Scalar(Strobe.Prf(32, false));
			}
		}

		public Scalar GetScalar() =>
			Sequence().First();

		internal ScalarVector GetScalarVector() =>
			new ScalarVector(Sequence().Take(SecretCount));
	}
}
