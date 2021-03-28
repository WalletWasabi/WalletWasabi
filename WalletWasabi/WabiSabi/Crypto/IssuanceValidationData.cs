using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.WabiSabi.Crypto
{
	public record IssuanceValidationData
	{
		internal IssuanceValidationData(long amount, Scalar r, GroupElement ma)
		{
			Amount = amount;
			Randomness = r;
			Ma = ma;
		}

		/// <summary>
		/// Amount committed in the pedersen commitment (<see cref="Ma">Ma</see>).
		/// </summary>
		public long Amount { get; }

		/// <summary>
		/// Randomness used as blinding factor in the pedersen commitment (<see cref="Ma">Ma</see>).
		/// </summary>
		public Scalar Randomness { get; }

		/// <summary>
		/// Pedersen commitment to the amount used to request the credential.
		/// </summary>
		public GroupElement Ma { get; }
	}
}
