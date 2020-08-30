using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto
{
	public class Term
	{		public Term(Scalar scalar, GroupElement groupElement)
		{
			Scalar = scalar;
			GroupElement = groupElement;
		}
        
		public Scalar Scalar { get; }
		public GroupElement GroupElement { get; }
		public GroupElement Eval() => Scalar * GroupElement;
	}
}
