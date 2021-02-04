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
		public ConnectionConfirmationResponse(IEnumerable<RegistrationResponseMessage> zeroCredentials, IEnumerable<RegistrationResponseMessage> realCredentials)
		{
			ZeroCredentials = zeroCredentials;
			RealCredentials = realCredentials;
		}

		public IEnumerable<RegistrationResponseMessage> ZeroCredentials { get; }

		public IEnumerable<RegistrationResponseMessage> RealCredentials { get; }
	}
}
