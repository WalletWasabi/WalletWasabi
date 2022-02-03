using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Models;

public class Alice
{
	public Alice(Coin coin, OwnershipProof ownershipProof, Round round, Guid id, bool isPayingZeroCoordinationFee)
	{
		// TODO init syntax?
		Round = round;
		Coin = coin;
		OwnershipProof = ownershipProof;
		Id = id;
		IsPayingZeroCoordinationFee = isPayingZeroCoordinationFee;
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
	public bool IsPayingZeroCoordinationFee { get; } = false;

	public long CalculateRemainingVsizeCredentials(int maxRegistrableSize) => maxRegistrableSize - TotalInputVsize;

	public Money CalculateRemainingAmountCredentials(FeeRate feeRate, CoordinationFeeRate coordinationFeeRate) =>
		Coin.EffectiveValue(feeRate, IsPayingZeroCoordinationFee ? CoordinationFeeRate.Zero : coordinationFeeRate);

	public void SetDeadlineRelativeTo(TimeSpan connectionConfirmationTimeout)
	{
		// Have alice timeouts a bit sooner than the timeout of connection confirmation phase.
		Deadline = DateTimeOffset.UtcNow + (connectionConfirmationTimeout * 0.9);
	}
}
