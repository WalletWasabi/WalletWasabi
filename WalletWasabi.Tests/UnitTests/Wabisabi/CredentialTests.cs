using System;
using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi
{
	public class CredentialTests
	{
		[Fact]
		public void Foo()
		{
			// Split 1 BTC into 0.1, 0.1, 0.8
			var numberOfCredentials = 2;
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);
			var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), numberOfCredentials, rnd);
			var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

			// Input Reg
			var (zeroCredentialRequest, zeroValidationData) = client.CreateRequestForZeroAmount();
			var zeroCredentialResponse = issuer.HandleRequest(zeroCredentialRequest);
			client.HandleResponse(zeroCredentialResponse, zeroValidationData);

			// Connection Conf
			var (credentialRequest, validationData) = client.CreateRequest(new[] { Money.Satoshis(1), Money.Satoshis(9) }, Array.Empty<Credential>());
			var credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);

			(zeroCredentialRequest, zeroValidationData) = client.CreateRequestForZeroAmount();
			zeroCredentialResponse = issuer.HandleRequest(zeroCredentialRequest);
			client.HandleResponse(zeroCredentialResponse, zeroValidationData);

			// Output Reg
			(credentialRequest, validationData) = client.CreateRequest(new[] { Money.Satoshis(1), Money.Satoshis(8) }, client.Credentials.Valuable);
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			var d1 = credentialRequest.DeltaAmount;

			(credentialRequest, validationData) = client.CreateRequest(new[] { Money.Satoshis(1), Money.Satoshis(7) }, client.Credentials.Valuable);
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			var d2 = credentialRequest.DeltaAmount;

			(credentialRequest, validationData) = client.CreateRequest(new[] { Money.Satoshis(1), Money.Satoshis(6) }, client.Credentials.Valuable);
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			var d3 = credentialRequest.DeltaAmount;

			(credentialRequest, validationData) = client.CreateRequest(Array.Empty<Money>(), client.Credentials.Valuable.Take(1));
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			var d4 = credentialRequest.DeltaAmount;

			(credentialRequest, validationData) = client.CreateRequest(Array.Empty<Money>(), client.Credentials.Valuable.Take(1));
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			var d5 = credentialRequest.DeltaAmount;
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CredentialIssuance()
		{
			var numberOfCredentials = 3;
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);

			var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), numberOfCredentials, rnd);

			{
				// Null request. This requests `numberOfCredentials` zero-value credentials.
				var (credentialRequest, validationData) = client.CreateRequestForZeroAmount();

				Assert.True(credentialRequest.IsNullRequest);
				Assert.Equal(numberOfCredentials, credentialRequest.Requested.Count());
				var requested = credentialRequest.Requested.ToArray();
				Assert.Empty(requested[0].BitCommitments);
				Assert.Empty(requested[1].BitCommitments);
				Assert.Empty(requested[2].BitCommitments);
				Assert.Equal(Money.Zero, credentialRequest.DeltaAmount);

				// Issuer part.
				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);
				Assert.Equal(numberOfCredentials, client.Credentials.ZeroValue.Count());
				Assert.Empty(client.Credentials.Valuable);
				var issuedCredential = client.Credentials.ZeroValue.First();
				Assert.True(issuedCredential.Amount.IsZero);
			}

			{
				var present = client.Credentials.ZeroValue.Take(numberOfCredentials);
				var (credentialRequest, validationData) = client.CreateRequest(new[] { Money.Coins(1) }, present);

				Assert.False(credentialRequest.IsNullRequest);
				var credentialRequested = credentialRequest.Requested.ToArray();
				Assert.Equal(numberOfCredentials, credentialRequested.Length);
				Assert.NotEmpty(credentialRequested[0].BitCommitments);
				Assert.NotEmpty(credentialRequested[1].BitCommitments);

				// Issuer part.
				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);
				var issuedCredential = Assert.Single(client.Credentials.Valuable);
				Assert.Equal(new Scalar(100_000_000), issuedCredential.Amount);

				Assert.Equal(2, client.Credentials.ZeroValue.Count());
				Assert.Equal(3, client.Credentials.All.Count());
			}

			{
				var valuableCredential = client.Credentials.Valuable.Take(1);
				var amounts = Enumerable.Repeat(Money.Coins(0.5m), 2);
				var (credentialRequest, validationData) = client.CreateRequest(amounts, valuableCredential);

				Assert.False(credentialRequest.IsNullRequest);
				var requested = credentialRequest.Requested.ToArray();
				Assert.Equal(numberOfCredentials, requested.Length);
				Assert.NotEmpty(requested[0].BitCommitments);
				Assert.NotEmpty(requested[1].BitCommitments);
				Assert.Equal(Money.Zero, credentialRequest.DeltaAmount);

				// Issuer part.
				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);
				var credentials = client.Credentials.All.ToArray();
				Assert.NotEmpty(credentials);
				Assert.Equal(3, credentials.Length);

				var valuableCredentials = client.Credentials.Valuable.ToArray();
				Assert.Equal(new Scalar(50_000_000), valuableCredentials[0].Amount);
				Assert.Equal(new Scalar(50_000_000), valuableCredentials[1].Amount);
			}

			{
				var client0 = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), numberOfCredentials, rnd);
				var (credentialRequest, validationData) = client0.CreateRequestForZeroAmount();

				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);
				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse, validationData);

				(credentialRequest, validationData) = client0.CreateRequest(new[] { Money.Coins(1m) }, Enumerable.Empty<Credential>());

				credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse, validationData);

				(credentialRequest, validationData) = client0.CreateRequest(Array.Empty<Money>(), client0.Credentials.Valuable);

				credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse, validationData);
				Assert.NotEmpty(client0.Credentials.All);
				Assert.Equal(numberOfCredentials, client0.Credentials.All.Count());
			}
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void InvalidCredentialRequests()
		{
			var numberOfCredentials = 3;
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);

			var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);
			{
				var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), numberOfCredentials, rnd);

				// Null request. This requests `numberOfCredentials` zero-value credentials.
				var (credentialRequest, validationData) = client.CreateRequestForZeroAmount();

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);

				var (validCredentialRequest, _) = client.CreateRequest(Array.Empty<Money>(), client.Credentials.ZeroValue.Take(1));

				// Test incorrect number of presentations (one instead of 3.)
				var presented = validCredentialRequest.Presented.ToArray();
				var invalidCredentialRequest = new RegistrationRequestMessage(
					validCredentialRequest.DeltaAmount,
					new[] { presented[0] }, // Should present 3 credentials.
					validCredentialRequest.Requested,
					validCredentialRequest.Proofs);

				var ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(invalidCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.InvalidNumberOfPresentedCredentials, ex.ErrorCode);
				Assert.Equal("3 credential presentations were expected but 1 were received.", ex.Message);

				// Test incorrect number of presentations (0 instead of 3.)
				presented = credentialRequest.Presented.ToArray();
				invalidCredentialRequest = new RegistrationRequestMessage(
					Money.Coins(2),
					Array.Empty<CredentialPresentation>(), // Should present 3 credentials.
					validCredentialRequest.Requested,
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(invalidCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.InvalidNumberOfPresentedCredentials, ex.ErrorCode);
				Assert.Equal("3 credential presentations were expected but 0 were received.", ex.Message);

				(validCredentialRequest, _) = client.CreateRequest(Array.Empty<Money>(), client.Credentials.All);

				// Test incorrect number of credential requests.
				invalidCredentialRequest = new RegistrationRequestMessage(
					validCredentialRequest.DeltaAmount,
					validCredentialRequest.Presented,
					validCredentialRequest.Requested.Take(1),
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(invalidCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.InvalidNumberOfRequestedCredentials, ex.ErrorCode);
				Assert.Equal("3 credential requests were expected but 1 were received.", ex.Message);

				// Test incorrect number of credential requests.
				invalidCredentialRequest = new RegistrationRequestMessage(
					Money.Coins(2),
					Array.Empty<CredentialPresentation>(),
					validCredentialRequest.Requested.Take(1),
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(invalidCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.InvalidNumberOfRequestedCredentials, ex.ErrorCode);
				Assert.Equal("3 credential requests were expected but 1 were received.", ex.Message);

				// Test invalid range proof.
				var requested = validCredentialRequest.Requested.ToArray();

				invalidCredentialRequest = new RegistrationRequestMessage(
					validCredentialRequest.DeltaAmount,
					validCredentialRequest.Presented,
					new[] { requested[0], requested[1], new IssuanceRequest(requested[2].Ma, new[] { GroupElement.Infinity }) },
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(invalidCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.InvalidBitCommitment, ex.ErrorCode);
			}

			{
				var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), numberOfCredentials, rnd);
				var (validCredentialRequest, validationData) = client.CreateRequestForZeroAmount();

				// Test invalid proofs.
				var proofs = validCredentialRequest.Proofs.ToArray();
				proofs[0] = proofs[1];
				var invalidCredentialRequest = new RegistrationRequestMessage(
					validCredentialRequest.DeltaAmount,
					validCredentialRequest.Presented,
					validCredentialRequest.Requested,
					proofs);

				var ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(invalidCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.CoordinatorReceivedInvalidProofs, ex.ErrorCode);
			}

			{
				var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), numberOfCredentials, rnd);
				var (validCredentialRequest, validationData) = client.CreateRequestForZeroAmount();

				var credentialResponse = issuer.HandleRequest(validCredentialRequest);
				client.HandleResponse(credentialResponse, validationData);

				(validCredentialRequest, validationData) = client.CreateRequest(Enumerable.Empty<Money>(), client.Credentials.All);

				issuer.HandleRequest(validCredentialRequest);
				var ex = Assert.Throws<WabiSabiException>(() => issuer.HandleRequest(validCredentialRequest));
				Assert.Equal(WabiSabiErrorCode.SerialNumberAlreadyUsed, ex.ErrorCode);
			}
		}
	}
}
