using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Coordinator.Models;

public class Alice
{
	public Alice(Coin coin, OwnershipProof ownershipProof, Round round, Guid id)
	{
		// TODO init syntax?
		Round = round;
		Coin = coin;
		OwnershipProof = ownershipProof;
		Id = id;
	}

	public Round Round { get; }
	public Guid Id { get; }
	public DateTimeOffset Deadline { get; set; } = DateTimeOffset.UtcNow;
	public Coin Coin { get; }
	public OwnershipProof OwnershipProof { get; }
	public Money TotalInputAmount => Coin.Amount;
	public int TotalInputVsize => Coin.ScriptPubKey.EstimateInputVsize();

	public bool ConfirmedConnection { get; set; } = false;
	public bool ReadyToSign { get; set; }

	public long CalculateRemainingVsizeCredentials(int maxRegistrableSize) => maxRegistrableSize - TotalInputVsize;

	public Money CalculateRemainingAmountCredentials(FeeRate feeRate) =>
		Coin.EffectiveValue(feeRate);

	public void SetDeadlineRelativeTo(TimeSpan connectionConfirmationTimeout)
	{
		// Have alice timeouts a bit sooner than the timeout of connection confirmation phase.
		Deadline = DateTimeOffset.UtcNow + (connectionConfirmationTimeout * 0.9);
	}
}
