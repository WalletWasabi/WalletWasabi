using NBitcoin;

namespace WalletWasabi.WabiSabi.Models
{
	public record InputWitnessPair(
		uint InputIndex,
		WitScript Witness
	);
}
