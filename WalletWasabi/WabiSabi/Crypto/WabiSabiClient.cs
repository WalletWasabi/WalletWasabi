using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Crypto
{
	/// <summary>
	/// Provides the methods for creating <see cref="CredentialsRequest">unified WabiSabi credential registration request messages</see>
	/// and for handling the <see cref="CredentialsResponse">credential registration responses</see> received from the coordinator.
	/// </summary>
	public class WabiSabiClient
	{
		public WabiSabiClient(
			CredentialIssuerParameters credentialIssuerParameters,
			WasabiRandom randomNumberGenerator,
			ulong maxAmount,
			CredentialPool? credentialPool = null)
		{
			MaxAmount = maxAmount;
			RangeProofWidth = (int)Math.Ceiling(Math.Log2(MaxAmount));
			RandomNumberGenerator = Guard.NotNull(nameof(randomNumberGenerator), randomNumberGenerator);
			CredentialIssuerParameters = Guard.NotNull(nameof(credentialIssuerParameters), credentialIssuerParameters);
			Credentials = credentialPool ?? new CredentialPool();
		}

		public ulong MaxAmount { get; }

		public int RangeProofWidth { get; }

		public int NumberOfCredentials => ProtocolConstants.CredentialNumber;

		private CredentialIssuerParameters CredentialIssuerParameters { get; }

		private WasabiRandom RandomNumberGenerator { get; }

		/// <summary>
		/// The credentials pool containing the available credentials.
		/// </summary>
		public CredentialPool Credentials { get; }

		/// <summary>
		/// Creates a <see cref="CredentialsRequest">credential registration request messages</see>
		/// for requesting `k` zero-value credentials.
		/// </summary>
		/// <remarks>
		/// The request messages created by CreateRequestForZeroAmount are called null requests. The first
		/// registration request message that has to be sent to the coordinator is a null request, in this
		/// way the coordinator issues `k` zero-value credentials that can be used in following requests.
		/// </remarks>
		public ZeroCredentialsRequestData CreateRequestForZeroAmount()
		{
			var credentialsToRequest = new IssuanceRequest[NumberOfCredentials];
			var knowledge = new Knowledge[NumberOfCredentials];
			var validationData = new IssuanceValidationData[NumberOfCredentials];

			for (var i = 0; i < NumberOfCredentials; i++)
			{
				var randomness = RandomNumberGenerator.GetScalar(allowZero: false);
				var ma = randomness * Generators.Gh;

				knowledge[i] = ProofSystem.ZeroProofKnowledge(ma, randomness);
				credentialsToRequest[i] = new IssuanceRequest(ma, Enumerable.Empty<GroupElement>());
				validationData[i] = new IssuanceValidationData(0, randomness, ma);
			}

			var transcript = BuildTransnscript(isNullRequest: true);

			return new(
				new ZeroCredentialsRequest(
					credentialsToRequest,
					ProofSystem.Prove(transcript, knowledge, RandomNumberGenerator)),
				new CredentialsResponseValidation(
					transcript,
					Enumerable.Empty<Credential>(),
					validationData));
		}

		/// <summary>
		/// Creates a <see cref="RealCredentialsRequest">credential registration request messages</see>
		/// for requesting `k` non-zero-value credentials.
		/// </summary>
		/// <param name="amountsToRequest">List of amounts requested in credentials.</param>
		/// <param name="credentialsToPresent">List of credentials to be presented to the coordinator.</param>
		/// <returns>
		/// A tuple containing the registration request message instance and the registration validation data
		/// to be used to validate the coordinator response message (the issued credentials).
		/// </returns>
		public RealCredentialsRequestData CreateRequest(
			IEnumerable<long> amountsToRequest,
			IEnumerable<Credential> credentialsToPresent)
		{
			// Make sure we request always the same number of credentials
			var credentialAmountsToRequest = amountsToRequest.ToList();
			var missingCredentialRequests = NumberOfCredentials - amountsToRequest.Count();
			for (var i = 0; i < missingCredentialRequests; i++)
			{
				credentialAmountsToRequest.Add(0);
			}

			// Make sure we present always the same number of credentials (except for Null requests)
			var missingCredentialPresent = NumberOfCredentials - credentialsToPresent.Count();

			var alreadyPresentedZeroCredentials = credentialsToPresent.Where(x => x.Amount.IsZero);
			var availableZeroCredentials = Credentials.ZeroValue.Except(alreadyPresentedZeroCredentials);

			// This should not be possible
			var availableZeroCredentialCount = availableZeroCredentials.Count();
			if (availableZeroCredentialCount < missingCredentialPresent)
			{
				throw new WabiSabiCryptoException(
					WabiSabiCryptoErrorCode.NotEnoughZeroCredentialToFillTheRequest,
					$"{missingCredentialPresent} credentials are missing but there are only {availableZeroCredentialCount} zero-value credentials available.");
			}

			credentialsToPresent = credentialsToPresent.Concat(availableZeroCredentials.Take(missingCredentialPresent)).ToList();
			var macsToPresent = credentialsToPresent.Select(x => x.Mac);
			if (macsToPresent.Distinct().Count() < macsToPresent.Count())
			{
				throw new WabiSabiCryptoException(WabiSabiCryptoErrorCode.CredentialToPresentDuplicated);
			}

			var zs = new List<Scalar>();
			var knowledgeToProve = new List<Knowledge>();
			var presentations = new List<CredentialPresentation>();
			foreach (var credential in credentialsToPresent)
			{
				var z = RandomNumberGenerator.GetScalar();
				var presentation = credential.Present(z);
				presentations.Add(presentation);
				knowledgeToProve.Add(ProofSystem.ShowCredentialKnowledge(presentation, z, credential, CredentialIssuerParameters));
				zs.Add(z);
			}

			// Generate RangeProofs (or ZeroProof) for each requested credential
			var credentialsToRequest = new IssuanceRequest[NumberOfCredentials];
			var validationData = new IssuanceValidationData[NumberOfCredentials];
			for (var i = 0; i < NumberOfCredentials; i++)
			{
				var amount = credentialAmountsToRequest[i];
				var scalarAmount = new Scalar((ulong)amount);

				var randomness = RandomNumberGenerator.GetScalar(allowZero: false);
				var ma = ProofSystem.PedersenCommitment(scalarAmount, randomness);

				var (rangeKnowledge, bitCommitments) = ProofSystem.RangeProofKnowledge(scalarAmount, randomness, RangeProofWidth, RandomNumberGenerator);
				knowledgeToProve.Add(rangeKnowledge);

				var credentialRequest = new IssuanceRequest(ma, bitCommitments);
				credentialsToRequest[i] = credentialRequest;
				validationData[i] = new IssuanceValidationData(amount, randomness, ma);
			}

			// Generate Balance Proof
			var sumOfZ = zs.Sum();
			var cr = credentialsToPresent.Select(x => x.Randomness).Sum();
			var r = validationData.Select(x => x.Randomness).Sum();
			var deltaR = cr + r.Negate();

			var balanceKnowledge = ProofSystem.BalanceProofKnowledge(sumOfZ, deltaR);
			knowledgeToProve.Add(balanceKnowledge);

			var transcript = BuildTransnscript(isNullRequest: false);
			return new(
				new RealCredentialsRequest(
					amountsToRequest.Sum() - credentialsToPresent.Sum(x => (long)x.Amount.ToUlong()),
					presentations,
					credentialsToRequest,
					ProofSystem.Prove(transcript, knowledgeToProve, RandomNumberGenerator)),
				new CredentialsResponseValidation(
					transcript,
					credentialsToPresent,
					validationData));
		}

		/// <summary>
		/// Handles the registration response received from the coordinator.
		/// </summary>
		/// <remarks>
		/// Verifies the registration response message proofs, creates the credentials based on the issued MACs and
		/// finally updates the credentials pool by removing those credentials that were presented and by adding
		/// those that were issued.
		/// </remarks>
		/// <param name="registrationResponse">The registration response message received from the coordinator.</param>
		/// <param name="registrationValidationData">The state data required to validate the issued credentials and the proofs.</param>
		public IEnumerable<Credential> HandleResponse(CredentialsResponse registrationResponse, CredentialsResponseValidation registrationValidationData)
		{
			Guard.NotNull(nameof(registrationResponse), registrationResponse);
			Guard.NotNull(nameof(registrationValidationData), registrationValidationData);

			var issuedCredentialCount = registrationResponse.IssuedCredentials.Count();
			var requestedCredentialCount = registrationValidationData.Requested.Count();
			if (issuedCredentialCount != NumberOfCredentials)
			{
				throw new WabiSabiCryptoException(
					WabiSabiCryptoErrorCode.IssuedCredentialNumberMismatch,
					$"{issuedCredentialCount} issued but {requestedCredentialCount} were requested.");
			}

			var credentials = registrationValidationData.Requested.Zip(registrationResponse.IssuedCredentials)
				.Select(x => (Requested: x.First, Issued: x.Second))
				.ToArray();

			var statements = credentials
				.Select(x => ProofSystem.IssuerParametersStatement(CredentialIssuerParameters, x.Issued, x.Requested.Ma));

			var areCorrectlyIssued = ProofSystem.Verify(registrationValidationData.Transcript, statements, registrationResponse.Proofs);
			if (!areCorrectlyIssued)
			{
				throw new WabiSabiCryptoException(WabiSabiCryptoErrorCode.ClientReceivedInvalidProofs);
			}

			var credentialReceived = credentials.Select(x =>
				new Credential(new Scalar((ulong)x.Requested.Amount), x.Requested.Randomness, x.Issued));

			Credentials.UpdateCredentials(credentialReceived, registrationValidationData.Presented);
			return credentialReceived;
		}

		private Transcript BuildTransnscript(bool isNullRequest)
		{
			var label = $"UnifiedRegistration/{NumberOfCredentials}/{isNullRequest}";
			var encodedLabel = Encoding.UTF8.GetBytes(label);
			return new Transcript(encodedLabel);
		}
	}
}
