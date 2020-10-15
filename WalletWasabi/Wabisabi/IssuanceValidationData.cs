using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Wabisabi
{
	public class IssuanceValidationData
	{
		public IssuanceValidationData(Money amount, Scalar r, GroupElement ma)
		{
			Amount = amount;
			Randomness = r;
			Ma = ma;
		}

		public Money Amount { get; }

		public Scalar Randomness { get; }

		public GroupElement Ma { get; }
	}
}
