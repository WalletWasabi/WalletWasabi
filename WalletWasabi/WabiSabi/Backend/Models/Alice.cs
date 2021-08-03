using NBitcoin;
using Nito.AsyncEx;
using System;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class Alice
	{
		public Alice(Coin coin, OwnershipProof ownershipProof, Round round)
		{
			// TODO init syntax?
			Round = round;
			Coin = coin;
			OwnershipProof = ownershipProof;
			Id = CalculateHash();
		}

		public Round Round { get; }
		public AsyncLock AsyncLock { get; } = new();
		public uint256 Id { get; }
		public DateTimeOffset Deadline { get; set; } = DateTimeOffset.UtcNow;
		public Coin Coin { get; }
		public OwnershipProof OwnershipProof { get; }
		public Money TotalInputAmount => Coin.Amount;
		public int TotalInputVsize => Coin.ScriptPubKey.EstimateInputVsize();

		public bool ConfirmedConnection { get; set; } = false;
		public bool ReadyToSign { get; set; }

		public long CalculateRemainingVsizeCredentials(int maxRegistrableSize) => maxRegistrableSize - TotalInputVsize;

		public Money CalculateRemainingAmountCredentials(FeeRate feeRate) => Coin.EffectiveValue(feeRate);

		public void SetDeadline(TimeSpan inputTimeout)
		{
			Deadline = DateTimeOffset.UtcNow + inputTimeout;
		}

		private uint256 CalculateHash() => CalculateHash(Coin, OwnershipProof);

		public static uint256 CalculateHash(Coin coin, OwnershipProof ownershipProof)
			=> StrobeHasher.Create(ProtocolConstants.AliceStrobeDomain)
				.Append(ProtocolConstants.AliceCoinTxOutStrobeLabel, coin.TxOut)
				.Append(ProtocolConstants.AliceCoinOutpointStrobeLabel, coin.Outpoint)
				.Append(ProtocolConstants.AliceOwnershipProofStrobeLabel, ownershipProof)
				.GetHash();
	}
}
