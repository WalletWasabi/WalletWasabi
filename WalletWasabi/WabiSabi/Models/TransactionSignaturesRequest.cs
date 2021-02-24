using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class TransactionSignaturesRequest
	{
		public TransactionSignaturesRequest(Guid roundId, IEnumerable<InputWitnessPair> inputWitnessPairs)
		{
			RoundId = roundId;
			InputWitnessPairs = inputWitnessPairs;
		}

		public Guid RoundId { get; }
		public IEnumerable<InputWitnessPair> InputWitnessPairs { get; }
	}
}
