using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi
{
	public class CredentialTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CorrectRangeProof()
		{
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);

			var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);
			var issuer = new CredentialIssuer(sk, rnd, 4300000000000);
			Assert.Equal(42, client.RangeProofWidth);
			Assert.Equal(42, issuer.RangeProofWidth);

			client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4400000000001);
			issuer = new CredentialIssuer(sk, rnd, 4400000000001);
			Assert.Equal(43, client.RangeProofWidth);
			Assert.Equal(43, issuer.RangeProofWidth);
		}

#if false
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void Splitting()
		{
			// Split 10 sats into 1, 1, 1, 1, 6.
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);
			var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);
			var issuer = new CredentialIssuer(sk, rnd, 4300000000000);

			// Input Reg
			var (zeroCredentialRequest, zeroValidationData) = client.CreateRequestForZeroAmount();
			var zeroCredentialResponse = issuer.HandleRequest(zeroCredentialRequest);
			client.HandleResponse(zeroCredentialResponse, zeroValidationData);

			// Connection Conf
			var (credentialRequest, validationData) = client.CreateRequest(new[] { 1L, 9L }, Array.Empty<Credential>());
			(zeroCredentialRequest, zeroValidationData) = client.CreateRequestForZeroAmount();

			Assert.Equal(10, credentialRequest.Delta);
			zeroCredentialResponse = issuer.HandleRequest(zeroCredentialRequest);
			var credentialResponse = issuer.HandleRequest(credentialRequest);

			client.HandleResponse(zeroCredentialResponse, zeroValidationData);
			client.HandleResponse(credentialResponse, validationData);

			// Output Reg
			(credentialRequest, validationData) = client.CreateRequest(new[] { 1L, 8L }, client.Credentials.TakeValuable());
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			Assert.Equal(-1, credentialRequest.Delta);

			(credentialRequest, validationData) = client.CreateRequest(new[] { 1L, 7L }, client.Credentials.TakeValuable());
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			Assert.Equal(-1, credentialRequest.Delta);

			(credentialRequest, validationData) = client.CreateRequest(new[] { 1L, 6L }, client.Credentials.TakeValuable());
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			Assert.Equal(-1, credentialRequest.Delta);

			(credentialRequest, validationData) = client.CreateRequest(Array.Empty<long>(), client.Credentials.Valuable.Where(x => x.Amount == Scalar.One).Take(1));
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			Assert.Equal(-1, credentialRequest.Delta);

			(credentialRequest, validationData) = client.CreateRequest(Array.Empty<long>(), client.Credentials.Valuable.Take(1));
			credentialResponse = issuer.HandleRequest(credentialRequest);
			client.HandleResponse(credentialResponse, validationData);
			Assert.Equal(-6, credentialRequest.Delta);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CredentialIssuance()
		{
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);

			var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);

			{
				// Null request. This requests `numberOfCredentials` zero-value credentials.
				var (credentialRequest, validationData) = client.CreateRequestForZeroAmount();

				Assert.True(credentialRequest.IsNullRequest);
				Assert.Equal(ProtocolConstants.CredentialNumber, credentialRequest.Requested.Count());
				var requested = credentialRequest.Requested.ToArray();
				Assert.Empty(requested[0].BitCommitments);
				Assert.Empty(requested[1].BitCommitments);
				Assert.Equal(0, credentialRequest.Delta);

				// Issuer part.
				var issuer = new CredentialIssuer(sk, rnd, 4300000000000);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				var valuableCredentials = client.HandleResponse(credentialResponse, validationData);
				Assert.Empty(valuableCredentials);
				var issuedCredential = client.Credentials.TakeZeroValue().First();
				Assert.True(issuedCredential.Amount.IsZero);
			}

			{
				var present = client.Credentials.TakeZeroValue();
				var (credentialRequest, validationData) = client.CreateRequest(new[] { 100_000_000L }, present);

				Assert.False(credentialRequest.IsNullRequest);
				var credentialRequested = credentialRequest.Requested.ToArray();
				Assert.Equal(ProtocolConstants.CredentialNumber, credentialRequested.Length);
				Assert.NotEmpty(credentialRequested[0].BitCommitments);
				Assert.NotEmpty(credentialRequested[1].BitCommitments);

				// Issuer part.
				var issuer = new CredentialIssuer(sk, rnd, 4300000000000);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);
				var issuedCredential = Assert.Single(client.Credentials.Valuable);
				Assert.Equal(new Scalar(100_000_000), issuedCredential.Amount);

				Assert.Single(client.Credentials.ZeroValue);
				Assert.Equal(2, client.Credentials.All.Count());
			}

			{
				var valuableCredential = client.Credentials.Valuable.Take(1);
				var amounts = Enumerable.Repeat(50_000_000L, 2);
				var (credentialRequest, validationData) = client.CreateRequest(amounts, valuableCredential);

				Assert.False(credentialRequest.IsNullRequest);
				var requested = credentialRequest.Requested.ToArray();
				Assert.Equal(ProtocolConstants.CredentialNumber, requested.Length);
				Assert.NotEmpty(requested[0].BitCommitments);
				Assert.NotEmpty(requested[1].BitCommitments);
				Assert.Equal(0, credentialRequest.Delta);

				// Issuer part.
				var issuer = new CredentialIssuer(sk, rnd, 4300000000000);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);
				var credentials = client.Credentials.All.ToArray();
				Assert.NotEmpty(credentials);
				Assert.Equal(2, credentials.Length);

				var valuableCredentials = client.Credentials.Valuable.ToArray();
				Assert.Equal(new Scalar(50_000_000), valuableCredentials[0].Amount);
				Assert.Equal(new Scalar(50_000_000), valuableCredentials[1].Amount);
			}

			{
				var client0 = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);
				(CredentialsRequest credentialRequest, CredentialsResponseValidation validationData) = client0.CreateRequestForZeroAmount();

				var issuer = new CredentialIssuer(sk, rnd, 4300000000000);
				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse, validationData);

				(credentialRequest, validationData) = client0.CreateRequest(new[] { 1L }, Enumerable.Empty<Credential>());

				credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse, validationData);

				(credentialRequest, validationData) = client0.CreateRequest(Array.Empty<long>(), client0.Credentials.Valuable);

				credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse, validationData);
				Assert.NotEmpty(client0.Credentials.All);
				Assert.Equal(ProtocolConstants.CredentialNumber, client0.Credentials.All.Count());
			}
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void InvalidCredentialRequests()
		{
			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);

			var issuer = new CredentialIssuer(sk, rnd, 4300000000000);
			{
				var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);

				// Null request. This requests `numberOfCredentials` zero-value credentials.
				var (credentialRequest, validationData) = client.CreateRequestForZeroAmount();

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse, validationData);

				var (validCredentialRequest, _) = client.CreateRequest(Array.Empty<long>(), client.Credentials.ZeroValue.Take(1));

				// Test incorrect number of presentations (one instead of 3.)
				var presented = validCredentialRequest.Presented.ToArray();
				var invalidCredentialRequest = new RealCredentialsRequest(
					validCredentialRequest.Delta,
					new[] { presented[0] }, // Should present 3 credentials.
					validCredentialRequest.Requested,
					validCredentialRequest.Proofs);

				var ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(invalidCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.InvalidNumberOfPresentedCredentials, ex.ErrorCode);
				Assert.Equal("2 credential presentations were expected but 1 were received.", ex.Message);

				// Test incorrect number of presentations (0 instead of 3.)
				presented = credentialRequest.Presented.ToArray();
				invalidCredentialRequest = new RealCredentialsRequest(
					Money.Coins(2),
					Array.Empty<CredentialPresentation>(), // Should present 3 credentials.
					validCredentialRequest.Requested,
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(invalidCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.InvalidNumberOfPresentedCredentials, ex.ErrorCode);
				Assert.Equal("2 credential presentations were expected but 0 were received.", ex.Message);

				(validCredentialRequest, _) = client.CreateRequest(Array.Empty<long>(), client.Credentials.All);

				// Test incorrect number of credential requests.
				invalidCredentialRequest = new RealCredentialsRequest(
					validCredentialRequest.Delta,
					validCredentialRequest.Presented,
					validCredentialRequest.Requested.Take(1),
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(invalidCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.InvalidNumberOfRequestedCredentials, ex.ErrorCode);
				Assert.Equal("2 credential requests were expected but 1 were received.", ex.Message);

				// Test incorrect number of credential requests.
				invalidCredentialRequest = new RealCredentialsRequest(
					Money.Coins(2),
					Array.Empty<CredentialPresentation>(),
					validCredentialRequest.Requested.Take(1),
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(invalidCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.InvalidNumberOfRequestedCredentials, ex.ErrorCode);
				Assert.Equal("2 credential requests were expected but 1 were received.", ex.Message);

				// Test invalid range proof.
				var requested = validCredentialRequest.Requested.ToArray();

				invalidCredentialRequest = new RealCredentialsRequest(
					validCredentialRequest.Delta,
					validCredentialRequest.Presented,
					new[] { requested[0], new IssuanceRequest(requested[1].Ma, new[] { GroupElement.Infinity }) },
					validCredentialRequest.Proofs);

				ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(invalidCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.InvalidBitCommitment, ex.ErrorCode);
			}

			{
				var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);
				var (validCredentialRequest, validationData) = client.CreateRequestForZeroAmount();

				// Test invalid proofs.
				var proofs = validCredentialRequest.Proofs.ToArray();
				proofs[0] = proofs[1];
				var invalidCredentialRequest = new ZeroCredentialsRequest(
					validCredentialRequest.Requested,
					proofs);

				var ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(invalidCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.CoordinatorReceivedInvalidProofs, ex.ErrorCode);
			}

			{
				var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);
				(CredentialsRequest validCredentialRequest, CredentialsResponseValidation validationData) = client.CreateRequestForZeroAmount();

				var credentialResponse = issuer.HandleRequest(validCredentialRequest);
				client.HandleResponse(credentialResponse, validationData);

				(validCredentialRequest, validationData) = client.CreateRequest(Enumerable.Empty<long>(), client.Credentials.All);

				issuer.HandleRequest(validCredentialRequest);
				var ex = Assert.Throws<WabiSabiCryptoException>(() => issuer.PrepareResponse(validCredentialRequest));
				Assert.Equal(WabiSabiCryptoErrorCode.SerialNumberAlreadyUsed, ex.ErrorCode);
			}
		}
#endif
	}
}
