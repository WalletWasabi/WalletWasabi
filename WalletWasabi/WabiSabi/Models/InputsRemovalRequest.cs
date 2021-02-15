using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputsRemovalRequest
	{
		public InputsRemovalRequest(Guid aliceId, Guid roundId)
		{
			AliceId = aliceId;
			RoundId = roundId;
		}

		public Guid AliceId { get; }
		public Guid RoundId { get; }
	}
}
