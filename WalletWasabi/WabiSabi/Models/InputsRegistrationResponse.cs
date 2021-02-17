using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputsRegistrationResponse
	{
		public InputsRegistrationResponse(Guid aliceId, CredentialsResponse amountCredentials, CredentialsResponse weightCredentials)
		{
			AliceId = aliceId;
			AmountCredentials = amountCredentials;
			WeightCredentials = weightCredentials;
		}

		public Guid AliceId { get; }
		public CredentialsResponse AmountCredentials { get; }
		public CredentialsResponse WeightCredentials { get; }
	}
}
