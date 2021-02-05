using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Models
{
	public class OutputRegistrationRequest
	{
		public OutputRegistrationRequest(Guid roundId, TxOut output, IEnumerable<Credential> presentedAmountCredentials, IEnumerable<Credential> presentedWeighCredentials)
		{
			RoundId = roundId;
			Output = output;
			PresentedAmountCredentials = presentedAmountCredentials;
			PresentedWeighCredentials = presentedWeighCredentials;
		}

		public Guid RoundId { get; }
		public TxOut Output { get; }
		public IEnumerable<Credential> PresentedAmountCredentials { get; }
		public IEnumerable<Credential> PresentedWeighCredentials { get; }
	}
}
