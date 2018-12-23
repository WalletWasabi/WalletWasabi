﻿using NBitcoin;
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

		public Money OutputSumWithoutCoordinatorFeeAndDenomination { get; }

		public IEnumerable<Coin> Inputs { get; }

		public BitcoinAddress ChangeOutputAddress { get; }

		public string BlindedOutputScriptHex { get; }

		public Money GetChangeAmount(Money denomination, Money coordinatorFee) => OutputSumWithoutCoordinatorFeeAndDenomination - denomination - coordinatorFee;

		public AliceState State { get; set; }

		public Alice(IEnumerable<Coin> inputs, Money networkFeeToPay, BitcoinAddress changeOutputAddress, string blindedOutputScriptHex)
		{
			Inputs = Guard.NotNullOrEmpty(nameof(inputs), inputs);
			NetworkFeeToPay = Guard.NotNull(nameof(networkFeeToPay), networkFeeToPay);
			BlindedOutputScriptHex = Guard.NotNullOrEmptyOrWhitespace(nameof(blindedOutputScriptHex), blindedOutputScriptHex);

			ChangeOutputAddress = Guard.NotNull(nameof(changeOutputAddress), changeOutputAddress);
			LastSeen = DateTimeOffset.UtcNow;

			UniqueId = Guid.NewGuid();

			InputSum = inputs.Sum(x => x.Amount);

			OutputSumWithoutCoordinatorFeeAndDenomination = InputSum - NetworkFeeToPay;

			State = AliceState.InputsRegistered;
		}
	}
}
