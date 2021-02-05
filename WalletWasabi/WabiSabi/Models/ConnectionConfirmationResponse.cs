using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class ConnectionConfirmationResponse
	{
		public ConnectionConfirmationResponse(RegistrationResponseMessage amountCredentials, RegistrationResponseMessage weightCredentials)
		{
			AmountCredentials = amountCredentials;
			WeightCredentials = weightCredentials;
		}

		public RegistrationResponseMessage AmountCredentials { get; }
		public RegistrationResponseMessage WeightCredentials { get; }
	}
}
