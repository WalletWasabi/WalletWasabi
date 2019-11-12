using NBitcoin;
using NBitcoin.Crypto;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Client.Rounds
{
	public class ClientRound
	{
		public RoundStateResponse State { get; set; }

		public ClientRoundRegistration Registration { get; set; }

		public long RoundId => State.RoundId;

		public IEnumerable<SmartCoin> CoinsRegistered => Registration?.CoinsRegistered ?? Enumerable.Empty<SmartCoin>();

		public ClientRound(RoundStateResponse state)
		{
			State = Guard.NotNull(nameof(state), state);
			ClearRegistration(); // shortcut for initializing variables
		}

		public void ClearRegistration()
		{
			Registration?.Dispose();
			Registration = null;
		}
	}
}
