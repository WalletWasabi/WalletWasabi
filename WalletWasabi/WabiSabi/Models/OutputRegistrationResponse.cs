using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public class OutputRegistrationResponse
	{
		public OutputRegistrationResponse(string unsignedTransactionSecret, CredentialsResponse amountCredentials, CredentialsResponse weightCredentials)
		{
			UnsignedTransactionSecret = unsignedTransactionSecret;
			AmountCredentials = amountCredentials;
			WeightCredentials = weightCredentials;
		}

		public string UnsignedTransactionSecret { get; }
		public CredentialsResponse AmountCredentials { get; }
		public CredentialsResponse WeightCredentials { get; }
	}
}
