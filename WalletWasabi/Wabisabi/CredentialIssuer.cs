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
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wabisabi
{
	public class CredentialIssuer
	{
		public CredentialIssuer(CoordinatorSecretKey sk, int numberOfCredentials, WasabiRandom randomNumberGenerator)
		{
			CoordinatorSecretKey = Guard.NotNull(nameof(sk), sk);
			NumberOfCredentials = Guard.InRangeAndNotNull(nameof(numberOfCredentials), numberOfCredentials, 1, 100);
			CoordinatorParameters = CoordinatorSecretKey.ComputeCoordinatorParameters();
			RandomNumberGenerator = Guard.NotNull(nameof(randomNumberGenerator), randomNumberGenerator);
		}

		// Keeps track of the used serial numbers. This is part of 
		// the double-spending prevention mechanism.
		private HashSet<GroupElement> SerialNumbers { get; } = new HashSet<GroupElement>();
		
		// Canary test check to ensure credential balance is never negative
		private Money Balance { get; set; } = Money.Zero;

		private WasabiRandom RandomNumberGenerator { get; }

		private CoordinatorSecretKey CoordinatorSecretKey { get; }
		
		private CoordinatorParameters CoordinatorParameters { get; }

		// Gets the number of credential that has to be requested/presented
		// This parameter is called `k` in the wabisabi paper.
		public int NumberOfCredentials { get; }

		public RegistrationResponse HandleRequest(RegistrationRequest registrationRequest)
		{
			Guard.NotNull(nameof(registrationRequest), registrationRequest);

			var requested = registrationRequest.Requested ?? Enumerable.Empty<IssuanceRequest>();
			var presented = registrationRequest.Presented ?? Enumerable.Empty<CredentialPresentation>();

			var requestedCount = requested.Count();  
			if (requestedCount != NumberOfCredentials)
			{
				throw new WabiSabiException(
					WabiSabiErrorCode.InvalidNumberOfRequestedCredentials, 
					$"{NumberOfCredentials} credential requests were expected but {requestedCount} were received.");
			}

			var presentedCount = presented.Count();
			var requiredNumberOfPresentations = registrationRequest.IsNullRequest ? 0 : NumberOfCredentials;
			if (presentedCount != requiredNumberOfPresentations)
			{
				throw new WabiSabiException(
					WabiSabiErrorCode.InvalidNumberOfPresentedCredentials, 
					$"{requiredNumberOfPresentations} credential presentations were expected but {presentedCount} were received.");
			}

			// Don't allow balance to go negative. In case this goes below zero 
			// then there is a problem somewhere because this should not be possible.
			if (Balance + registrationRequest.DeltaAmount < Money.Zero)
			{
				throw new WabiSabiException(WabiSabiErrorCode.NegativeBalance);
			}

			// Check all the serial numbers are unique. Note that this is checked separately from
			// ensuring that they haven't been used before, because even presenting a previously
			// unused credential more than once in the same request is still a double spend.
			var rangeProofWidth = registrationRequest.IsNullRequest ? 0 : Constants.RangeProofWidth;
			var allRangeProofsAreCorrectSize = requested.All(x => x.BitCommitments.Count() == rangeProofWidth);
			if (!allRangeProofsAreCorrectSize)
			{
				throw new WabiSabiException(WabiSabiErrorCode.InvalidBitCommitment);
			} 

			// Check all the serial numbers are unique.
			if (registrationRequest.AreThereDuplicatedSerialNumbers)
			{
				throw new WabiSabiException(WabiSabiErrorCode.SerialNumberDuplicated);
			}

			var statements = new List<Statement>();
			foreach (var presentation in presented)
			{
				// Calculate Z using coordinator secret.
				var Z = presentation.ComputeZ(CoordinatorSecretKey);

				// Add the credential presentation to the statements to be verified.
				statements.Add(ProofSystem.ShowCredential(presentation, Z, CoordinatorParameters));

				// Check if the serial numbers have been used before. Note that
				// the serial numbers have not yet been verified at this point, but a
				// request with an invalid proof and a used serial number should also be
				// rejected.
				if (SerialNumbers.Contains(presentation.S))
				{
					throw new WabiSabiException(WabiSabiErrorCode.SerialNumberAlreadyUsed, $"Serial number reused {presentation.S}");
				}
			}

			foreach (var credentialRequest in requested)
			{
				statements.Add(registrationRequest.IsNullRequest
					? ProofSystem.ZeroProof(credentialRequest.Ma)
					: ProofSystem.RangeProof(credentialRequest.Ma, credentialRequest.BitCommitments));
			}

			// Balance proof
			if (!registrationRequest.IsNullRequest)
			{
				var sumCa = presented.Select(x => x.Ca).Sum();
				var sumMa = requested.Select(x => x.Ma).Sum();

				// A positive Delta_a means the requested credential amounts are larger
				// than the presented ones (i.e. input registration, and a negative
				// balance correspond to output registration). The equation requires a
				// commitment to 0, so the sum of the presented attributes and the
				// negated requested attributes are tweaked by delta_a.
				var absAmountDelta = new Scalar(registrationRequest.DeltaAmount.Abs());
				var deltaA = registrationRequest.DeltaAmount < Money.Zero ? absAmountDelta.Negate() : absAmountDelta;
				var balanceTweak = deltaA * Generators.Gg;
				statements.Add(ProofSystem.BalanceProof(balanceTweak + sumCa - sumMa));
			}

			// Construct response.
			var transcript = BuildTransnscript(registrationRequest.IsNullRequest);

			// Verify all statements.
			var areProofsValid = Verifier.Verify(transcript, statements, registrationRequest.Proofs);
			if (!areProofsValid)
			{
				throw new WabiSabiException(WabiSabiErrorCode.CoordinatorReceivedInvalidProofs);
			}

			// Issue credentials.
			var credentials = requested.Select(x => IssueCredential(x.Ma, RandomNumberGenerator.GetScalar())).ToArray();

			var proofs = Prover.Prove(transcript, credentials.Select(x => x.Knowledge), RandomNumberGenerator);
			var macs = credentials.Select(x => x.Mac);
			var response = new RegistrationResponse(macs, proofs);

			// Register the serial numbers to prevent credential reuse.
			foreach (var presentation in presented)
			{
				SerialNumbers.Add(presentation.S);
			}
			Balance += registrationRequest.DeltaAmount;

			return response;
		}

		private (MAC Mac, Knowledge Knowledge) IssueCredential(GroupElement ma,  Scalar t)
		{
			var sk = CoordinatorSecretKey;
			var mac = MAC.ComputeMAC(sk, ma, t);
			var knowledge = ProofSystem.IssuerParameters(mac, ma, sk);
			return (mac, knowledge);
		}

		private Transcript BuildTransnscript(bool isNullRequest)
		{
			var label = $"UnifiedRegistration/{NumberOfCredentials}/{isNullRequest}";
			var encodedLabel = Encoding.UTF8.GetBytes(label);
			return new Transcript(encodedLabel);
		}
	}
}