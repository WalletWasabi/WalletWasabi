using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class OutputRegistrationResponse
	{
		public OutputRegistrationResponse(byte[] unsignedTransactionSecret)
		{
			UnsignedTransactionSecret = unsignedTransactionSecret;
		}

		public byte[] UnsignedTransactionSecret { get; }
	}
}
