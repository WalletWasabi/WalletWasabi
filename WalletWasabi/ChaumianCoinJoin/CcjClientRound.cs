using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;

namespace WalletWasabi.ChaumianCoinJoin
{
	public class CcjClientRound
	{
		public CcjRunningRoundState State { get; set; }
		
		public List<MixCoin> CoinsRegistered { get; }
		
		public Guid? AliceUniqueId { get; set; }

		public HdPubKey ChangeOutput { get; set; }
		public HdPubKey ActiveOutput { get; set; }

		public byte[] UnblindedSignature { get; set; }

		public string RoundHash { get; set; }

		public CcjClientRound(CcjRunningRoundState state)
		{
			State = Guard.NotNull(nameof(state), state);
			CoinsRegistered = new List<MixCoin>();
			ClearRegistration(); // shortcut for initializing variables
		}

		public void ClearRegistration()
		{
			CoinsRegistered.Clear();
			AliceUniqueId = null;
			ChangeOutput = null;
			ActiveOutput = null;
			UnblindedSignature = null;
			RoundHash = null;
		}
	}
}
