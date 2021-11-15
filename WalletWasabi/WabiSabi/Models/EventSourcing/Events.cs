using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.EventSourcing
{
	public record RoundCreated(Round Round);
	public record InputAdded(Coin Coin, OwnershipProof OwnershipProof);
	public record OutputAdded(TxOut Output);
	public record WitnessAdded(InputWitnessPair InputWitnessPairs);
	public record StatePhaseChanged(Phase NewPhase);
}
