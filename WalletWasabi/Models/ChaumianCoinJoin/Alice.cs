using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Alice
	{
		public DateTimeOffset LastSeen { get; set; }

		public Guid UniqueId { get; }

		public Money InputSum { get; }

		public Money NetworkFeeToPay { get; }

		public IEnumerable<Coin> Inputs { get; }

		public BitcoinAddress ChangeOutputAddress { get; }

		public AliceState State { get; set; }

		public uint256[] BlindedOutputScripts { get; }

		public IEnumerable<BigInteger> BlindedOutputSignatures { get; set; }

		public Alice(IEnumerable<Coin> inputs, Money networkFeeToPay, BitcoinAddress changeOutputAddress, IEnumerable<uint256> blindedOutputScripts)
		{
			Inputs = Guard.NotNullOrEmpty(nameof(inputs), inputs);
			NetworkFeeToPay = Guard.NotNull(nameof(networkFeeToPay), networkFeeToPay);

			BlindedOutputScripts = blindedOutputScripts?.ToArray() ?? new uint256[0];

			ChangeOutputAddress = Guard.NotNull(nameof(changeOutputAddress), changeOutputAddress);
			LastSeen = DateTimeOffset.UtcNow;

			UniqueId = Guid.NewGuid();

			InputSum = inputs.Sum(x => x.Amount);

			State = AliceState.InputsRegistered;

			BlindedOutputSignatures = new List<BigInteger>();
		}
	}
}
