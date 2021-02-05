using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputsRegistrationResponse
	{
		public InputsRegistrationResponse(Guid aliceId, RegistrationResponseMessage amountCredentials, RegistrationResponseMessage weightCredentials)
		{
			AliceId = aliceId;
			AmountCredentials = amountCredentials;
			WeightCredentials = weightCredentials;
		}

		public Guid AliceId { get; }
		public RegistrationResponseMessage AmountCredentials { get; }
		public RegistrationResponseMessage WeightCredentials { get; }
	}
}
