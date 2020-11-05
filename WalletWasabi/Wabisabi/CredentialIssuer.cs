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

namespace WalletWasabi.Wabisabi
{
	/// <summary>
	/// Issues anonymous credentials using the coordinator's secret key.
	/// </summary>
	/// <remarks>
	/// CredentialIssuer is the coordinator's component used to issue anonymous credentials 
	/// requested by a WabiSabi client. This means this component abstracts receives  
	/// <see cref="RegistrationRequestMessage">RegistrationRequests</see>, validates the requested
	/// amounts are in the valid range, serial numbers are not duplicated nor reused,
	/// and finally issues the credentials using the coordinator's secret key (and also
	/// proving to the WabiSabi client that the credentials were issued with the right 
	/// key).
	///
	/// Note that this is a stateful component because it needs to keep track of the 
	/// presented credentials' serial numbers in order to prevent credential reuse.
	/// Additionally it keeps also track of the `balance` in order to make sure it never 
	/// issues credentials for more money than the total presented amount. All this means
	/// that the same instance has to be used for a given round (the coordinator needs to
	/// maintain only one instance of this class per round)
	/// 
	/// About replay requests: a replay request is a <see cref="RegistrationRequestMessage">request</see>
	/// that has already been seen before. These kind of requests can be the result of misbehaving
	/// clients or simply clients using a retry communication mechanism. 
	/// Reply requests are not handled by this component and they have to be handled by a different
	/// component. A good solution is to use a caching feature where the request fingerprint is
	/// used as a key, in this way using a standard solution it is possible to respond with the exact
	/// same valid credentials to the client without performance penalties.
	/// </remarks>
	public class CredentialIssuer
	{
		/// <summary>
		/// Initializes a new instance of the CredentialIssuer class.
		/// </summary>
		/// <param name="coordinatorSecretKey">The <see cref="CoordinatorSecretKey">coordinator's secret key</see> used to issue the credentials.</param>
		/// <param name="numberOfCredentials">The number of credentials that the protocol handles in each request/response.</param>
		/// <param name="randomNumberGenerator">The random number generator.</param>
		public CredentialIssuer(CoordinatorSecretKey coordinatorSecretKey, int numberOfCredentials, WasabiRandom randomNumberGenerator)
		{
			CoordinatorSecretKey = Guard.NotNull(nameof(coordinatorSecretKey), coordinatorSecretKey);
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

		/// <summary>
		/// Gets the number of credentials that have to be requested/presented
		/// This parameter is called `k` in the WabiSabi paper.
		/// </summary>
		public int NumberOfCredentials { get; }

		/// <summary>
		/// Process the <see cref="RegistrationRequestMessage">credentials registration requests</see> and
		/// issues the credentials.
		/// </summary>
		/// <param name="registrationRequest">The request containing the credentials presentations, credential requests and the proofs.</param>
		/// <returns>The <see cref="RegistrationResponseMessage">registration response</see> containing the requested credentials and the proofs.</returns>
		/// <exception cref="WabiSabiException">Error code: <see cref="WabiSabiErrorCode">WabiSabiErrorCode</see></exception>
		public RegistrationResponseMessage HandleRequest(RegistrationRequestMessage registrationRequest)
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

			// Check that the range proofs are of the appropriate bitwidth
			var rangeProofWidth = registrationRequest.IsNullRequest ? 0 : Constants.RangeProofWidth;
			var allRangeProofsAreCorrectSize = requested.All(x => x.BitCommitments.Count() == rangeProofWidth);
			if (!allRangeProofsAreCorrectSize)
			{
				throw new WabiSabiException(WabiSabiErrorCode.InvalidBitCommitment);
			}

			// Check all the serial numbers are unique. Note that this is checked separately from
			// ensuring that they haven't been used before, because even presenting a previously
			// unused credential more than once in the same request is still a double spend.
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
				statements.Add(ProofSystem.ShowCredentialStatement(presentation, Z, CoordinatorParameters));

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
					? ProofSystem.ZeroProofStatement(credentialRequest.Ma)
					: ProofSystem.RangeProofStatement(credentialRequest.Ma, credentialRequest.BitCommitments));
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
				statements.Add(ProofSystem.BalanceProofStatement(balanceTweak + sumCa - sumMa));
			}

			var transcript = BuildTransnscript(registrationRequest.IsNullRequest);

			// Verify all statements.
			var areProofsValid = ProofSystem.Verify(transcript, statements, registrationRequest.Proofs);
			if (!areProofsValid)
			{
				throw new WabiSabiException(WabiSabiErrorCode.CoordinatorReceivedInvalidProofs);
			}

			// Issue credentials.
			var credentials = requested.Select(x => IssueCredential(x.Ma, RandomNumberGenerator.GetScalar())).ToArray();

			// Construct response.
			var proofs = ProofSystem.Prove(transcript, credentials.Select(x => x.Knowledge), RandomNumberGenerator);
			var macs = credentials.Select(x => x.Mac);
			var response = new RegistrationResponseMessage(macs, proofs);

			// Register the serial numbers to prevent credential reuse.
			foreach (var presentation in presented)
			{
				SerialNumbers.Add(presentation.S);
			}
			Balance += registrationRequest.DeltaAmount;

			return response;
		}

		private (MAC Mac, Knowledge Knowledge) IssueCredential(GroupElement ma, Scalar t)
		{
			var sk = CoordinatorSecretKey;
			var mac = MAC.ComputeMAC(sk, ma, t);
			var knowledge = ProofSystem.IssuerParametersKnowledge(mac, ma, sk);
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
