using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.StrobeProtocol;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class SyntheticPublicNoncesProvider
	{
		private readonly Strobe128 _strobe;

		public SyntheticPublicNoncesProvider(Strobe128 strobe, IEnumerable<Scalar> secrets, WasabiRandom random)
		{
			_strobe = strobe;
			
			// add secret inputs as key material
			foreach (var secret in secrets)
			{
				_strobe.Key(secret.ToBytes(), false);
			}

			_strobe.Key(random.GetBytes(32), false);
		}

		public IEnumerable<Scalar> Sequence()
		{
			while (true)
			{
				yield return new Scalar(_strobe.Prf(32, false));
			}
		}

		public Scalar GetScalar() =>
			Sequence().First();

		public ScalarVector GetScalar(uint len) =>
			new ScalarVector(Sequence().Take((int)len));
	}
}
