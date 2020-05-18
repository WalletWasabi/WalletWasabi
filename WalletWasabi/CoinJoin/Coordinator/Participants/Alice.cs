using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Coordinator.Participants
{
	public class Alice
	{
		public Alice(IEnumerable<SmartInput> smartInputs, Money networkFeeToPayAfterBaseDenomination, BitcoinAddress changeOutputAddress, IEnumerable<uint256> blindedOutputScripts)
		{
			SmartInputs = Guard.NotNullOrEmpty(nameof(smartInputs), smartInputs);
			NetworkFeeToPayAfterBaseDenomination = Guard.NotNull(nameof(networkFeeToPayAfterBaseDenomination), networkFeeToPayAfterBaseDenomination);

			BlindedOutputScripts = blindedOutputScripts?.ToArray() ?? Array.Empty<uint256>();

			ChangeOutputAddress = Guard.NotNull(nameof(changeOutputAddress), changeOutputAddress);
			LastSeen = DateTimeOffset.UtcNow;

			UniqueId = Guid.NewGuid();

			State = AliceState.InputsRegistered;

			BlindedOutputSignatures = Array.Empty<uint256>();
		}

		public DateTimeOffset LastSeen { get; set; }

		public Guid UniqueId { get; }

		public Money InputSum => Inputs.Sum(x => x.Amount);

		public Money NetworkFeeToPayAfterBaseDenomination { get; }

		public IEnumerable<SmartInput> SmartInputs { get; }
		public IEnumerable<Coin> Inputs => SmartInputs.Select(x => x.Coin);

		public BitcoinAddress ChangeOutputAddress { get; }

		public AliceState State { get; set; }

		public uint256[] BlindedOutputScripts { get; set; }

		public uint256[] BlindedOutputSignatures { get; set; }
	}
}
