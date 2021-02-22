using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputsRemovalRequest
	{
		public InputsRemovalRequest(Guid roundId, Guid aliceId)
		{
			RoundId = roundId;
			AliceId = aliceId;
		}

		public Guid RoundId { get; }
		public Guid AliceId { get; }
	}
}
