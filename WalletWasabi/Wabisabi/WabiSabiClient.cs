using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wabisabi
{
	public class WabiSabiClient
	{
		public WabiSabiClient(CoordinatorParameters coordinatorParameters, int numberOfCredentials, WasabiRandom randomNumberGenerator)
		{
			RandomNumberGenerator = Guard.NotNull(nameof(randomNumberGenerator), randomNumberGenerator);
			NumberOfCredentials = Guard.InRangeAndNotNull(nameof(numberOfCredentials), numberOfCredentials, 1, 100);
			CoordinatorParameters = Guard.NotNull(nameof(coordinatorParameters), coordinatorParameters);
		}

		private CoordinatorParameters CoordinatorParameters { get; }

		private WasabiRandom RandomNumberGenerator { get; }

		private int NumberOfCredentials { get; }

		public CredentialPool Credentials { get; } = new CredentialPool();

		public CredentialRegistrationFactory GetCredentialRegistrationFactory()
			=> new CredentialRegistrationFactory(NumberOfCredentials, Credentials, CoordinatorParameters, RandomNumberGenerator);

		public void HandleResponse(RegistrationResponse registrationResponse, RegistrationValidationData registrationValidationData)
		{
			Guard.NotNull(nameof(registrationResponse), registrationResponse);
			Guard.NotNull(nameof(registrationValidationData), registrationValidationData);

			var issuedCredentialCount = registrationResponse.IssuedCredentials.Count();
			var requestedCredentialCount = registrationValidationData.Requested.Count();
			if (issuedCredentialCount != NumberOfCredentials)
			{
				throw new WabiSabiException(
					WabiSabiErrorCode.IssuedCredentialNumberMismatch, 
					$"{issuedCredentialCount} issued but {requestedCredentialCount} were requested.");
			}

			var credentials = Enumerable
				.Zip(registrationValidationData.Requested, registrationResponse.IssuedCredentials)
				.Select(x => (Requested: x.First, Issued: x.Second))
				.ToArray();

			var statements = credentials
				.Select(x => ProofSystem.IssuerParameters(CoordinatorParameters, x.Issued, x.Requested.Ma));

			var areCorrectlyIssued = Verifier.Verify(registrationValidationData.Transcript, statements, registrationResponse.Proofs);
			if (!areCorrectlyIssued)
			{
				throw new WabiSabiException(WabiSabiErrorCode.ClientReceivedInvalidProofs);
			}

			var credentialReceived = credentials.Select(x => 
				new Credential(new Scalar((ulong)x.Requested.Amount.Satoshi), x.Requested.Randomness, x.Issued));

			Credentials.UpdateCredentials(credentialReceived, registrationValidationData.Presented);
		}
	}
}