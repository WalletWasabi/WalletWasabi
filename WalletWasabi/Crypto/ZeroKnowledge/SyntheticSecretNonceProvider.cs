using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge;

public class SyntheticSecretNonceProvider
{
	private readonly Strobe128 _strobe;
	private readonly int _secretCount;

	public SyntheticSecretNonceProvider(Strobe128 strobe, IEnumerable<Scalar> secrets, WasabiRandom random)
	{
		Guard.NotNullOrEmpty(nameof(secrets), secrets);
		_strobe = strobe;
		_secretCount = secrets.Count();

		// add secret inputs as key material
		foreach (var secret in secrets)
		{
			_strobe.Key(secret.ToBytes(), false);
		}

		_strobe.Key(random.GetBytes(32), false);
	}

	private IEnumerable<Scalar> Sequence()
	{
		while (true)
		{
			Scalar scalar;
			int overflow;
			do
			{
				scalar = new Scalar(_strobe.Prf(32, false), out overflow);
			}
			while (overflow != 0);
			yield return scalar;
		}
	}

	public Scalar GetScalar() =>
		Sequence().First();

	internal ScalarVector GetScalarVector() =>
		new(Sequence().Take(_secretCount));
}
