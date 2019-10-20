using NBitcoin;
using NBitcoin.Crypto;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.CoinJoin.Client
{
	public class CcjClientRound
	{
		public CcjRunningRoundState State { get; set; }

		public ClientRoundRegistration Registration { get; set; }

		public long RoundId => State.RoundId;

		public IEnumerable<SmartCoin> CoinsRegistered => Registration?.CoinsRegistered ?? Enumerable.Empty<SmartCoin>();

		public CcjClientRound(CcjRunningRoundState state)
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
