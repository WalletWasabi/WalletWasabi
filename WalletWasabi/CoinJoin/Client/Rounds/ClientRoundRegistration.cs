using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Client.Rounds
{
	public class ClientRoundRegistration
	{
		public ClientRoundRegistration(AliceClientBase aliceClient, IEnumerable<SmartCoin> coinsRegistereds, BitcoinAddress changeAddress)
		{
			AliceClient = Guard.NotNull(nameof(aliceClient), aliceClient);
			CoinsRegistered = Guard.NotNullOrEmpty(nameof(coinsRegistereds), coinsRegistereds);
			ChangeAddress = Guard.NotNull(nameof(changeAddress), changeAddress);

			ActiveOutputs = Enumerable.Empty<ActiveOutput>();
		}

		/// <summary>
		/// Gets or sets the RoundPhase that completed all the necessary actions in the phase.
		/// </summary>
		public RoundPhase CompletedPhase { get; set; }

		public BitcoinAddress ChangeAddress { get; }

		public IEnumerable<ActiveOutput> ActiveOutputs { get; set; }

		public IEnumerable<SmartCoin> CoinsRegistered { get; }

		public AliceClientBase AliceClient { get; }

		public bool IsPhaseActionsComleted(RoundPhase phase) => CompletedPhase >= phase;

		public void SetPhaseCompleted(RoundPhase phase)
		{
			if (!IsPhaseActionsComleted(phase))
			{
				CompletedPhase = phase;
			}
		}
	}
}
