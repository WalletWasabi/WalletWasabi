using NBitcoin;
using NBitcoin.Crypto;
using System.Collections.Generic;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class CcjClientRound
	{
		public CcjRunningRoundState State { get; set; }

		public List<SmartCoin> CoinsRegistered { get; }

		public AliceClient AliceClient { get; set; }

		public BitcoinAddress ChangeOutputAddress { get; set; }
		public BitcoinAddress ActiveOutputAddress { get; set; }
		public BitcoinAddress[] AdditionalActiveOutputAddresses { get; set; }

		public BlindSignature UnblindedSignature { get; set; }
		public BlindSignature[] AdditionalUnblindedSignatures { get; set; }

		/// <summary>
		/// Connection has been confirmed in ConnectionConfirmation Phase. Used to avoid duplicate Connection Confirmation requests.
		/// </summary>
		public bool ConnectionFinalConfirmed { get; set; }

		public bool Signed { get; set; }
		public bool PostedOutput { get; set; }

		public CcjClientRound(CcjRunningRoundState state)
		{
			State = Guard.NotNull(nameof(state), state);
			CoinsRegistered = new List<SmartCoin>();
			ClearRegistration(); // shortcut for initializing variables
		}

		public void ClearRegistration()
		{
			CoinsRegistered.Clear();
			ChangeOutputAddress = null;
			ActiveOutputAddress = null;
			AdditionalActiveOutputAddresses = null;
			UnblindedSignature = null;
			AdditionalUnblindedSignatures = null;
			ConnectionFinalConfirmed = false;
			AliceClient?.Dispose();
			AliceClient = null;
			Signed = false;
			PostedOutput = false;
		}
	}
}
