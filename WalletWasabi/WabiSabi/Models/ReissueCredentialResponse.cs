using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record ReissueCredentialResponse(
		CredentialsResponse RealAmountCredentials,
		CredentialsResponse ZeroAmountCredentials
	);
}
