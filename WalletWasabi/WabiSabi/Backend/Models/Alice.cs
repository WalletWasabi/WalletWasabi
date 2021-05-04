using NBitcoin;
using System;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.StrobeProtocol;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class Alice
	{
		public Alice(Coin coin, OwnershipProof ownershipProof)
		{
			// TODO init syntax?
			Coin = coin;
			OwnershipProof = ownershipProof;
			Id = CalculateHash();
		}

		public uint256 Id { get; }
		public DateTimeOffset Deadline { get; set; } = DateTimeOffset.UtcNow;
		public Coin Coin { get; }
		public OwnershipProof OwnershipProof { get; }
		public Money TotalInputAmount => Coin.Amount;
		public int TotalInputVsize => Coin.ScriptPubKey.EstimateInputVsize();

		public bool ConfirmedConnection { get; set; } = false;

		public long CalculateRemainingVsizeCredentials(uint maxRegistrableSize) => maxRegistrableSize - TotalInputVsize;

		public Money CalculateRemainingAmountCredentials(FeeRate feeRate) => TotalInputAmount - feeRate.GetFee(TotalInputVsize);

		public void SetDeadlineRelativeTo(TimeSpan connectionConfirmationTimeout)
		{
			// Have alice timeouts a bit sooner than the timeout of connection confirmation phase.
			Deadline = DateTimeOffset.UtcNow + (connectionConfirmationTimeout * 0.9);
		}

		private uint256 CalculateHash()
			=> StrobeHasher.Create(ProtocolConstants.AliceStrobeDomain)
				.Append(ProtocolConstants.AliceCoinTxOutStrobeLabel, Coin.TxOut)
				.Append(ProtocolConstants.AliceCoinOutpointStrobeLabel, Coin.Outpoint)
				.Append(ProtocolConstants.AliceOwnershipProofStrobeLabel, OwnershipProof)
				.GetHash();

		public static uint256 CalculateHash(Coin coin, BitcoinSecret bitcoinSecret, uint256 roundId)
		{
			var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
				bitcoinSecret.PrivateKey,
				new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundId));
			return new Alice(coin, ownershipProof).CalculateHash();
		}
	}
}
