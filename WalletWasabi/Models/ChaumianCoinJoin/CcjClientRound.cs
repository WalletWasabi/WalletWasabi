using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.WebClients.ChaumianCoinJoin;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class CcjClientRound
	{
		public CcjRunningRoundState State { get; set; }
		
		public List<SmartCoin> CoinsRegistered { get; }

		public AliceClient AliceClient { get; set; }

		public HdPubKey ChangeOutput { get; set; }
		public HdPubKey ActiveOutput { get; set; }

		public byte[] UnblindedSignature { get; set; }

		public string RoundHash { get; set; }

		public CcjClientRound(CcjRunningRoundState state)
		{
			State = Guard.NotNull(nameof(state), state);
			CoinsRegistered = new List<SmartCoin>();
			ClearRegistration(); // shortcut for initializing variables
		}

		public void ClearRegistration()
		{
			CoinsRegistered.Clear();
			ChangeOutput = null;
			ActiveOutput = null;
			UnblindedSignature = null;
			RoundHash = null;
			AliceClient?.Dispose();
			AliceClient = null;
		}
	}
}
