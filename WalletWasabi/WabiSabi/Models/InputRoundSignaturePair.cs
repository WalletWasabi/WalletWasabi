using NBitcoin;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputRoundSignaturePair(
		OutPoint Input,
		byte[] RoundSignature
	);
}
