using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public class ConnectionConfirmationResponse
	{
		public ConnectionConfirmationResponse(CredentialsResponse zeroAmountCredentials, CredentialsResponse zeroWeightCredentials, CredentialsResponse? realAmountCredentials = null, CredentialsResponse? realWeightCredentials = null)
		{
			ZeroAmountCredentials = zeroAmountCredentials;
			ZeroWeightCredentials = zeroWeightCredentials;
			RealAmountCredentials = realAmountCredentials;
			RealWeightCredentials = realWeightCredentials;
		}

		public CredentialsResponse ZeroAmountCredentials { get; }
		public CredentialsResponse ZeroWeightCredentials { get; }
		public CredentialsResponse? RealAmountCredentials { get; }
		public CredentialsResponse? RealWeightCredentials { get; }
	}
}
