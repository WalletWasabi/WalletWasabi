using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class TransactionSignaturesRequest
	{
		public TransactionSignaturesRequest(Guid roundId, Guid aliceId, IEnumerable<InputWitnessPair> inputWitnessPairs)
		{
			RoundId = roundId;
			AliceId = aliceId;
			InputWitnessPairs = inputWitnessPairs;
		}

		public Guid RoundId { get; }

		public Guid AliceId { get; }
		public IEnumerable<InputWitnessPair> InputWitnessPairs { get; }
	}
}
