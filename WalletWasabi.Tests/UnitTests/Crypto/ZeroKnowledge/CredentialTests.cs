using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class CredentialTests
	{
		[Fact]
		public void CredentialIssuance()
		{
			var rnd = new SecureRandom();
			var sk = new CoordinatorSecretKey(rnd);

			var issuer = new RoundCoordinator(sk, rnd);

			Assert.Equal(0, issuer.Ledger.Count);
			Assert.Equal(0, issuer.Balance);

			var client = new Client(issuer, rnd);

			Assert.True(0 == issuer.Balance);
			Assert.True(Scalar.Zero == client.Balance);

			// null credential should have been requested and issued
			Assert.Single(issuer.Ledger);

			client.UpdateBalance(7);

			Assert.Equal(7, issuer.Balance);
			Assert.True(new Scalar(7) == client.Balance);

			client.UpdateBalance(-4);

			Assert.Equal(3, issuer.Balance);
			Assert.True(new Scalar(3) == client.Balance);

			var client2 = new Client(issuer, rnd);

			Assert.Equal(3, issuer.Balance);
			Assert.True(new Scalar(3) == client.Balance);
			Assert.True(Scalar.Zero == client2.Balance);

			client2.UpdateBalance(5);

			Assert.Equal(8, issuer.Balance);
			Assert.True(new Scalar(3) == client.Balance);
			Assert.True(new Scalar(5) == client2.Balance);
		}
	}

	public class CredentialRequest
	{
		public CredentialRequest(GroupElement ma)
			: this(ma, new GroupElement[0])
		{
		}

		public CredentialRequest(GroupElement ma, IEnumerable<GroupElement> bitCommitments)
		{
			Ma = ma;
			BitCommitments = bitCommitments;
		}

		public GroupElement Ma { get; }
		public IEnumerable<GroupElement> BitCommitments { get; }

		// TODO public Scalar TNonce { get; }
	}

	public class RegistrationRequest
	{
		public RegistrationRequest(IEnumerable<CredentialRequest> requested, IEnumerable<Proof> proofs)
			: this(0, new CredentialPresentation[0], requested, proofs)
		{
		}

		public RegistrationRequest(int balance, IEnumerable<CredentialPresentation> presented, IEnumerable<CredentialRequest> requested, IEnumerable<Proof> proofs)
		{
			Balance = balance;
			Presented = presented;
			Requested = requested;
			Proofs = proofs;
		}

		public int Balance { get; }
		public IEnumerable<CredentialPresentation> Presented { get; }
		public IEnumerable<CredentialRequest> Requested { get; }
		public IEnumerable<Proof> Proofs { get; }

		// TODO conversion monotonicity proof w/ credentials of multiple types?

		public IEnumerable<GroupElement> Nullifiers { get => Presented.Select(x => x.S); }
	}

	public class RegistrationResponse
	{
		public RegistrationResponse(IEnumerable<MAC> issuedCredentials, IEnumerable<Proof> proofs)
		{
			IssuedCredentials = issuedCredentials;
			Proofs = proofs;
		}

		public IEnumerable<MAC> IssuedCredentials { get; }

		public IEnumerable<Proof> Proofs { get; }
	}

	public class StateTransition
	{
		public StateTransition(RegistrationRequest request, RegistrationResponse response)
		{
			Request = request;
			Response = response;
		}

		public RegistrationRequest Request { get; }

		public RegistrationResponse Response { get; }
	}

	public interface IPublicRoundAPI
	{
		public CoordinatorParameters CoordinatorParameters { get; }
		public int K { get; } // number of presentations/requests per registration
		public int Bits { get; } // width of range proofs
		public RegistrationResponse HandleRequest(RegistrationRequest request); // throws on errors
	}

	public class RoundCoordinator : IPublicRoundAPI
	{
		public RoundCoordinator(CoordinatorSecretKey sk, WasabiRandom random)
		{
			Ledger = new List<StateTransition>();
			CoordinatorSecretKey = sk;
			Random = random;
			K = 1; // >=2 for privacy (allows paralell composition)
			Bits = 3; // 1..32
		}

		public int K { get; }

		public int Bits { get; }

		public WasabiRandom Random { get; }

		public CoordinatorSecretKey CoordinatorSecretKey { get; }
		public List<StateTransition> Ledger { get; }

		public int Balance { get => Ledger.Select(x => x.Request.Balance).Sum(); }
		public CoordinatorParameters CoordinatorParameters { get => CoordinatorSecretKey.ComputeCoordinatorParameters(); }
		public IEnumerable<GroupElement> Nullifiers { get => Ledger.SelectMany(x => x.Request.Nullifiers); } // FIXME This should be probably optimized using a HashSet or something.

		public RegistrationResponse HandleRequest(RegistrationRequest registrationRequest)
		{
			// TODO DoS protection - verify PoW on t = H(H(roundId, tNonce)). H(roundId, tNonce) should be small (only if isNullRequest? always?)

			var isNullRequest = registrationRequest.Balance == 0 && registrationRequest.Presented.Count() == 0;
			var rangeProofWidth = isNullRequest ? 0 : Bits;

			Guard.Same(nameof(registrationRequest), K, registrationRequest.Requested.Count());
			Guard.Same(nameof(registrationRequest), isNullRequest ? 0 : K, registrationRequest.Presented.Count());

			var sk = CoordinatorSecretKey;

			var statements = new List<Statement>();

			foreach (var presentation in registrationRequest.Presented)
			{
				// Calculate Z using coordinator secret
				var Z = presentation.ComputeZ(CoordinatorSecretKey);

				statements.Add(ProofSystem.ShowCredential(presentation, Z, CoordinatorParameters));

				// Check if the serial numbers have it has been used before. Note that
				// the serial numbers have not yet been verified at this point, but a
				// request with an invalid proof and a used serial number should also be
				// rejected
				// TODO return idempotent response from Ledger if request exactly equals
				// this implies z should be derived deterministically in show, can be H(amount, randomness)
				Guard.False(nameof(presentation), Nullifiers.Contains(presentation.S));
			}

			foreach (var credentialRequest in registrationRequest.Requested)
			{
				Guard.Same(nameof(credentialRequest), credentialRequest.BitCommitments.Count(), rangeProofWidth);
				statements.Add(ProofSystem.RangeProof(credentialRequest.Ma, credentialRequest.BitCommitments));
			}

			// Balance proof
			if (!isNullRequest)
			{
				var presented = registrationRequest.Presented.Select(x => x.Ca).Sum();
				var requested = registrationRequest.Requested.Select(x => x.Ma).Sum();

				// A positive Delta_a means the requested credential amounts are larger
				// than the presented ones (i.e. input registration, and a negative
				// balance corresponds to output registration). The equation requires a
				// commitment to 0, so the sum of the presented attributes and the
				// negated requested attributes is tweaked by delta_a.
				// FIXME refactor? use IntToScalar? pass sign separately?
				var absAmountDelta = new Scalar((uint)Math.Abs(registrationRequest.Balance));
				var deltaA = registrationRequest.Balance < 0 ? absAmountDelta.Negate() : absAmountDelta;
				var balanceTweak = deltaA * Generators.Gg;
				statements.Add(ProofSystem.BalanceProof(balanceTweak + presented - requested));
			}

			// Don't allow balance to go negative
			// This should also probably be optimized.
			Guard.True(nameof(registrationRequest), Balance + registrationRequest.Balance >= 0);

			var transcript = new Transcript(new byte[0]); // FIXME label unified registration, K, isNullRequest

			// Verify all statements.
			Guard.True(nameof(registrationRequest), Verifier.Verify(transcript, statements, registrationRequest.Proofs));

			// Issue credentials.
			var credentials = registrationRequest.Requested.Select(x => IssueCredential(x.Ma, Random.GetScalar())).ToArray();

			// Construct response.
			var proofs = Prover.Prove(transcript, credentials.Select(x => x.Knowledge), Random);
			var macs = credentials.Select(x => x.Mac);
			var response = new RegistrationResponse(macs, proofs);

			// Log response.
			Ledger.Add(new StateTransition(registrationRequest, response)); // TODO fsync ;-)

			return response;
		}

		private (MAC Mac, Knowledge Knowledge) IssueCredential(GroupElement ma,  Scalar t)
		{
			var sk = CoordinatorSecretKey;
			var mac = MAC.ComputeMAC(sk, ma, t);
			var knowledge = ProofSystem.IssuerParameters(mac, ma, sk);
			return (mac, knowledge);
		}
	}

	public class Client
	{
		public Client(IPublicRoundAPI issuer, WasabiRandom random)
		{
			Issuer = issuer;

			var transcript = new Transcript(new byte[0]); // TODO label unified protocol, K, isNullRequest = true

			// FIXME handle K != 1
			var r = random.GetScalar();
			var Ma = r * Generators.Gh;
			var proofs = Prover.Prove(transcript, new[] { ProofSystem.ZeroProof(Ma, r) }, random);

			var nullRequest = new RegistrationRequest(new[] { new CredentialRequest(Ma) }, proofs);

			var response = issuer.HandleRequest(nullRequest);

			var statements = response.IssuedCredentials.Select(mac => ProofSystem.IssuerParameters(CoordinatorParameters, mac, Ma));

			Guard.True(nameof(response), Verifier.Verify(transcript, statements, response.Proofs));

			State = new Credential(Scalar.Zero, r, response.IssuedCredentials.First());
			Random = random;
		}

		public WasabiRandom Random { get; }

		public IPublicRoundAPI Issuer { get; }

		public CoordinatorParameters CoordinatorParameters { get => Issuer.CoordinatorParameters; }

		public Credential State { get; set; }

		public Scalar Balance { get => State.Amount; }

		public void UpdateBalance(int difference)
		{
			var delta_a = IntToScalar(difference);
			Scalar updatedBalance = Balance + delta_a;

			// show existing credential
			var z = Random.GetScalar();
			var credentialPresentation = State.Present(z);
			var showKnowledge = ProofSystem.ShowCredential(credentialPresentation, z, State, CoordinatorParameters);

			// TODO handle K > 1
			// generate a credential request for the new balance
			var r = Random.GetScalar();
			var Ma = updatedBalance * Generators.Gg + r * Generators.Gh;
			var (rangeKnowledge, bitCommitments) = ProofSystem.RangeProof(updatedBalance, r, Issuer.Bits, Random);
			var credentialRequest = new CredentialRequest(Ma, bitCommitments);

			// Generate a balance proof
			var balanceKnowledge = ProofSystem.BalanceProof(z, State.Randomness + r.Negate());

			var transcript = new Transcript(new byte[0]); // TODO label unified protocol, K, isNullRequest = true

			// TODO support K > 1, minimum possible: pad with 0s
			var proofs = Prover.Prove(transcript, new[] { showKnowledge, rangeKnowledge, balanceKnowledge
				}, Random);

			var request = new RegistrationRequest(difference, new[] { credentialPresentation }, new[] { credentialRequest }, proofs);

			// issue a new request and update the credential
			var response = Issuer.HandleRequest(request);

			// verify response
			var statements = response.IssuedCredentials.Select(mac => ProofSystem.IssuerParameters(CoordinatorParameters, mac, Ma));
			Guard.True(nameof(response), Verifier.Verify(transcript, statements, response.Proofs));

			// save new credential
			// TODO support K > 1
			var mac = response.IssuedCredentials.First();
			State = new Credential(updatedBalance, r, mac);
		}

		private static Scalar IntToScalar(int n)
		{
			// refactor int or long constructor for Scalar?
			// should really be long, not int but there's no ulong constructor either
			var s = new Scalar((uint)Math.Abs(n));
			return (n < 0) ? s.Negate() : s;
		}
	}
}
