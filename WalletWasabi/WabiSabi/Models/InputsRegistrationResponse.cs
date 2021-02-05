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
		public InputsRegistrationResponse(Guid aliceId, RegistrationResponseMessage credentials)
		{
			AliceId = aliceId;
			Credentials = credentials;
		}

		public Guid AliceId { get; }
		public RegistrationResponseMessage Credentials { get; }
	}
}
