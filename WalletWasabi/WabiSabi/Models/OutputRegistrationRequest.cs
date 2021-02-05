using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class OutputRegistrationRequest
	{
		public OutputRegistrationRequest(Guid roundId, TxOut output, RegistrationRequestMessage presentedAmountCredentials, RegistrationRequestMessage presentedWeighCredentials)
		{
			RoundId = roundId;
			Output = output;
			PresentedAmountCredentials = presentedAmountCredentials;
			PresentedWeighCredentials = presentedWeighCredentials;
		}

		public Guid RoundId { get; }
		public TxOut Output { get; }
		public RegistrationRequestMessage PresentedAmountCredentials { get; }
		public RegistrationRequestMessage PresentedWeighCredentials { get; }
	}
}
