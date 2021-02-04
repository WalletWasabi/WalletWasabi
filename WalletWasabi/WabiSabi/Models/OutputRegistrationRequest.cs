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
		public OutputRegistrationRequest(Guid roundId, TxOut output, IEnumerable<Credential> presentedCredentials)
		{
			RoundId = roundId;
			Output = output;
			PresentedCredentials = presentedCredentials;
		}

		public Guid RoundId { get; }

		public TxOut Output { get; }
		public IEnumerable<Credential> PresentedCredentials { get; }
	}
}
