using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Api
{
	public class WabiSabiClient : ICredentialRequestBuilder
	{
		private CoordinatorParameters CoordinatorParameters { get; }
		private readonly WasabiRandom Random;
		private readonly List<Money> CredentialsToRequest = new List<Money>(); 
		private readonly List<(Money Amount, CredentialIssuanceRequest Credential, Scalar r, Knowledge Knowledge)> CredentialsRequested = new List<(Money, CredentialIssuanceRequest, Scalar, Knowledge)>(); 
		private readonly List<(Credential Credential, Scalar z, Knowledge Knowledge)> CredentialsToPresent = new List<(Credential, Scalar, Knowledge)>();

		public WabiSabiClient(CoordinatorParameters coordinatorParameters, int numberOfCredentials, WasabiRandom rnd)
		{
			Random = Guard.NotNull(nameof(rnd), rnd);
			NumberOfCredentials = Guard.InRangeAndNotNull(nameof(numberOfCredentials), numberOfCredentials, 1, 100);
			CoordinatorParameters = Guard.NotNull(nameof(coordinatorParameters), coordinatorParameters);
		}

		private Money Balance => CredentialsToRequest.Sum() - Credentials.Sum(x => x.Amount.ToMoney());

		private bool IsNullRequest => Balance == Money.Zero && !CredentialsToPresent.Any();

		private int NumberOfCredentials { get; set; } = 1;

		public List<Credential> Credentials { get; } = new List<Credential>();

		public void HandleResponse(RegistrationResponse registrationResponse, Transcript transcript)
		{
			Guard.NotNull(nameof(registrationResponse), registrationResponse);

			var requestedCredentials = CredentialsRequested;
			var issuedCredentials = registrationResponse.IssuedCredentials;

			var issuedCredentialCount = issuedCredentials.Count();
			var requestedCredentialCount = requestedCredentials.Count();
			if (issuedCredentialCount != NumberOfCredentials)
			{
				throw new WabiSabiException(
					WabiSabiErrorCode.IssuedCredentialNumberMismatch, 
					$"{issuedCredentialCount} issued but {requestedCredentialCount} were requested.");
			}

			var credentials = Enumerable
				.Zip(requestedCredentials, issuedCredentials)
				.Select(x => new { Requested = x.First, Issued = x.Second })
				.ToArray();

			var statements = credentials
				.Select(x => ProofSystem.IssuerParameters(CoordinatorParameters, x.Issued, x.Requested.Credential.Ma));

			var areCorrectlyIssued = Verifier.Verify(transcript, statements, registrationResponse.Proofs);
			if (!areCorrectlyIssued)
			{
				throw new WabiSabiException(WabiSabiErrorCode.ClientReceivedInvalidProofs);
			}

			foreach (var presented in CredentialsToPresent)
			{
				Credentials.Remove(presented.Credential);
			}

			CredentialsToPresent.Clear();
			CredentialsRequested.Clear();
			CredentialsToRequest.Clear();

			var credentialReceived = credentials.Select(x => 
				new Credential(new Scalar((ulong)x.Requested.Amount.Satoshi), x.Requested.r, x.Issued));
			Credentials.AddRange(credentialReceived);
		}

		public ICredentialRequestBuilder AsCredentialRequestBuilder()
		{
			return this;
		} 

		ICredentialRequestBuilder ICredentialRequestBuilder.RequestCredentialFor(Money amount)
		{
			Guard.NotNull(nameof(amount), amount);
			Guard.InRangeAndNotNull(nameof(amount), amount, Money.Zero, Constants.MaximumCredentailAmount);

			CredentialsToRequest.Add(amount);

			return this;
		}

		ICredentialRequestBuilder ICredentialRequestBuilder.PresentCredentials(params Credential[] credentials)
		{
			Guard.NotNullOrEmpty(nameof(credentials), credentials);

			foreach (var credential in credentials)
			{
				var z = Random.GetScalar();
				if (!CredentialsToPresent.Any(x => x.Credential.Mac == credential.Mac))
				{
					var knowledge = ProofSystem.ShowCredential(credential.Present(z), z, credential, CoordinatorParameters);
					CredentialsToPresent.Add((credential, z, knowledge));
				}
			}
			return this;
		}

		RegistrationRequest ICredentialRequestBuilder.Build(Transcript transcript)
		{
			// Make sure we request always the same number of credentials
			var missingCredentialRequests = NumberOfCredentials - CredentialsToRequest.Count();
			for (var i = 0; i < missingCredentialRequests; i++)
			{
				CredentialsToRequest.Add(Money.Zero);
			}

			// Make sure we present always the same number of credentials (except for Null requests)
			var missingCredentialPresent = NumberOfCredentials - CredentialsToPresent.Count();

			if (!IsNullRequest && missingCredentialPresent > 0)
			{
				var zeroCredentials = Credentials.Where(x=>x.Amount.IsZero);
				var alreadyPresentedZeroCredentials = CredentialsToPresent.Select(x=>x.Credential).Where(x=>x.Amount.IsZero);
				var availableZeroCredentials = zeroCredentials.Except(alreadyPresentedZeroCredentials); 

				// This should not be possible 
				var availableZeroCredentialCount = availableZeroCredentials.Count(); 
				if ( availableZeroCredentialCount < missingCredentialPresent)
				{
					throw new WabiSabiException(
						WabiSabiErrorCode.NotEnoughZeroCredentialToFillTheRequest, 
						$"{missingCredentialPresent} credentials are missing but there are only {availableZeroCredentialCount} zero-value credentials available.");
				}

				ICredentialRequestBuilder me = this;
				me.PresentCredentials(availableZeroCredentials.Take(missingCredentialPresent).ToArray());
			}

			// Generate RangeProofs (or ZeroProof) for each requested credential
			foreach (var amount in CredentialsToRequest)
			{
				var attribute = Attribute.FromMoney(amount, Random);
				var scalarAmount = new Scalar((ulong)amount.Satoshi);

				if (IsNullRequest)
				{
					var rangeKnowledge = ProofSystem.ZeroProof(attribute.Ma, attribute.Randomness);

					var credentialRequest = new CredentialIssuanceRequest(attribute.Ma, Enumerable.Empty<GroupElement>());
					CredentialsRequested.Add((amount, credentialRequest, attribute.Randomness, rangeKnowledge));
				}
				else
				{
					var (rangeKnowledge, bitCommitments) = ProofSystem.RangeProof(scalarAmount, attribute.Randomness, Constants.RangeProofWidth, Random);

					var credentialRequest = new CredentialIssuanceRequest(attribute.Ma, bitCommitments);
					CredentialsRequested.Add((amount, credentialRequest, attribute.Randomness, rangeKnowledge));
				}
			}

			// Generate Balance Proof
			Knowledge balanceKnowledge;
			if (IsNullRequest)
			{
				var r = Random.GetScalar();
				var Ma = r * Generators.Gh;
				balanceKnowledge = ProofSystem.ZeroProof(Ma, r);
			}
			else
			{
				var balanceData = ComputeBalanceData();
				balanceKnowledge = ProofSystem.BalanceProof(balanceData.SumOfZ, balanceData.DeltaR);
			}

			var knowledgeToProve = 	CredentialsToPresent.Select(x => x.Knowledge)
				.Concat(CredentialsRequested.Select(x => x.Knowledge));

			if (!IsNullRequest)
			{
				knowledgeToProve = knowledgeToProve.Concat(new []{ balanceKnowledge });
			}

			return new RegistrationRequest(
				Balance, 
				CredentialsToPresent.Select(x => x.Credential.Present(x.z)), 
				CredentialsRequested.Select(x => x.Credential),
				Prover.Prove(transcript, knowledgeToProve, Random));
		}

		private (Scalar SumOfZ, Scalar DeltaR) ComputeBalanceData()
		{
			var sumOfZ = CredentialsToPresent.Select(x => x.z).Sum();
			var cr = CredentialsToPresent.Select(x => x.Credential.Randomness).Sum();
			var r = CredentialsRequested.Select(x => x.r).Sum();
			var deltaR = cr + r.Negate();
			return (sumOfZ, deltaR);
		}
	}
}