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
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wabisabi
{
	public class CredentialRegistrationFactory
	{
		internal CredentialRegistrationFactory(
			int numberOfCredentials, 
			CredentialPool credentialPool, 
			CoordinatorParameters coordinatorParameters, 
			WasabiRandom randomNumberGenerator)
		{
			NumberOfCredentials = numberOfCredentials;
			RandomNumberGenerator = randomNumberGenerator;
			CoordinatorParameters = coordinatorParameters;
			CredentialPool = credentialPool;
		}

		private int NumberOfCredentials { get; }

		private CoordinatorParameters CoordinatorParameters { get; }

		private CredentialPool CredentialPool { get; }
		 
		private WasabiRandom RandomNumberGenerator { get; }

		public (RegistrationRequest, RegistrationValidationData) CreateRequestForZeroAmount()
		{
			var credentialsToRequest = new IssuanceRequest[NumberOfCredentials];
			var knowledge = new Knowledge[NumberOfCredentials];
			var validationData = new IssuanceValidationData[NumberOfCredentials];

			for (var i = 0; i < NumberOfCredentials; i++)
			{
				var amount = Money.Zero;
				var attribute = Attribute.FromMoney(amount, RandomNumberGenerator.GetScalar(allowZero: false));

				knowledge[i] = ProofSystem.ZeroProof(attribute.Ma, attribute.Randomness);
				credentialsToRequest[i] = new IssuanceRequest(attribute.Ma, Enumerable.Empty<GroupElement>());
				validationData[i] = new IssuanceValidationData(amount, attribute.Randomness, attribute.Ma);
			}

			var transcript = BuildTransnscript(isNullRequest: true);

			return (
				new RegistrationRequest(
					Money.Zero,
					Enumerable.Empty<CredentialPresentation>(),
					credentialsToRequest,
					Prover.Prove(transcript, knowledge, RandomNumberGenerator)),
				new RegistrationValidationData(
					transcript,
					Enumerable.Empty<Credential>(),
					validationData));
		}

		public (RegistrationRequest, RegistrationValidationData) CreateRequest(
			IEnumerable<Money> amountsToRequest,
			IEnumerable<Credential> credentialsToPresent)
		{
			// Make sure we request always the same number of credentials
			var credentialAmountsToRequest = amountsToRequest.ToList();
			var missingCredentialRequests = NumberOfCredentials - amountsToRequest.Count();
			for (var i = 0; i < missingCredentialRequests; i++)
			{
				credentialAmountsToRequest.Add(Money.Zero);
			}

			// Make sure we present always the same number of credentials (except for Null requests)
			var missingCredentialPresent = NumberOfCredentials - credentialsToPresent.Count();

			var alreadyPresentedZeroCredentials = credentialsToPresent.Where(x => x.Amount.IsZero);
			var availableZeroCredentials = CredentialPool.ZeroValue.Except(alreadyPresentedZeroCredentials);

			// This should not be possible 
			var availableZeroCredentialCount = availableZeroCredentials.Count();
			if (availableZeroCredentialCount < missingCredentialPresent)
			{
				throw new WabiSabiException(
					WabiSabiErrorCode.NotEnoughZeroCredentialToFillTheRequest,
					$"{missingCredentialPresent} credentials are missing but there are only {availableZeroCredentialCount} zero-value credentials available.");
			}

			credentialsToPresent = credentialsToPresent.Concat(availableZeroCredentials.Take(missingCredentialPresent)).ToList();
			var macsToPresent = credentialsToPresent.Select(x => x.Mac);
			if (macsToPresent.Distinct().Count() < macsToPresent.Count())
			{
				throw new WabiSabiException(WabiSabiErrorCode.CredentialToPresentDuplicated);
			}

			var zs = new List<Scalar>();
			var knowledgeToProve = new List<Knowledge>();
			var presentations = new List<CredentialPresentation>();
			foreach (var credential in credentialsToPresent)
			{
				var z = RandomNumberGenerator.GetScalar();
				var presentation = credential.Present(z);
				presentations.Add(presentation);
				knowledgeToProve.Add(ProofSystem.ShowCredential(presentation, z, credential, CoordinatorParameters));
				zs.Add(z);
			}

			// Generate RangeProofs (or ZeroProof) for each requested credential
			var credentialsToRequest = new IssuanceRequest[NumberOfCredentials];
			var validationData = new IssuanceValidationData[NumberOfCredentials];
			for (var i = 0; i < NumberOfCredentials; i++)
			{
				var amount = credentialAmountsToRequest[i];
				var attribute = Attribute.FromMoney(amount, RandomNumberGenerator.GetScalar(allowZero: false));
				var scalarAmount = new Scalar((ulong)amount.Satoshi);

				var (rangeKnowledge, bitCommitments) = ProofSystem.RangeProof(scalarAmount, attribute.Randomness, Constants.RangeProofWidth, RandomNumberGenerator);
				knowledgeToProve.Add(rangeKnowledge);

				var credentialRequest = new IssuanceRequest(attribute.Ma, bitCommitments);
				credentialsToRequest[i] = credentialRequest;
				validationData[i] = new IssuanceValidationData(amount, attribute.Randomness, credentialRequest.Ma);
			}

			// Generate Balance Proof
			var sumOfZ = zs.Sum();
			var cr = credentialsToPresent.Select(x => x.Randomness).Sum();
			var r = validationData.Select(x => x.Randomness).Sum();
			var deltaR = cr + r.Negate();

			var balanceKnowledge = ProofSystem.BalanceProof(sumOfZ, deltaR);
			knowledgeToProve.Add(balanceKnowledge);

			var transcript = BuildTransnscript(isNullRequest: false);
			return (
				new RegistrationRequest(
					amountsToRequest.Sum() - credentialsToPresent.Sum(x => x.Amount.ToMoney()),
					presentations,
					credentialsToRequest,
					Prover.Prove(transcript, knowledgeToProve, RandomNumberGenerator)),
				new RegistrationValidationData(
					transcript,
					credentialsToPresent,
					validationData));
		}

		private Transcript BuildTransnscript(bool isNullRequest)
		{
			var label = $"UnifiedRegistration/{NumberOfCredentials}/{isNullRequest}";
			var encodedLabel = Encoding.UTF8.GetBytes(label);
			return new Transcript(encodedLabel);
		}
	}
}
