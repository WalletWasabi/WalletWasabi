using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class OutputRegistrationResponse
	{
		public OutputRegistrationResponse(byte[] unsignedTransactionSecret, RegistrationResponseMessage amountCredentials, RegistrationResponseMessage weightCredentials)
		{
			UnsignedTransactionSecret = unsignedTransactionSecret;
			AmountCredentials = amountCredentials;
			WeightCredentials = weightCredentials;
		}

		public byte[] UnsignedTransactionSecret { get; }
		public RegistrationResponseMessage AmountCredentials { get; }
		public RegistrationResponseMessage WeightCredentials { get; }
	}
}
