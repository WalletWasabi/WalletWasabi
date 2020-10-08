using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Api;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.Api
{
	public class CredentialTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CredentialIssuance()
		{
			var numberOfCredentials = 3;
			var rnd = new SecureRandom();
			var sk = new CoordinatorSecretKey(rnd);

			var client = new WabiSabiClient(sk.ComputeCoordinatorParameters(), numberOfCredentials, rnd);

			{
				// Null request. This requests `numberOfCredentials` zero-value credentials.
				var credentialRequest = client.AsCredentialRequestBuilder()
					.Build();

				Assert.True(credentialRequest.IsNullRequest);
				Assert.Equal(numberOfCredentials, credentialRequest.Requested.Count());
				var requested = credentialRequest.Requested.First();
				Assert.Empty(requested.BitCommitments);
				Assert.Equal(Money.Zero, credentialRequest.DeltaAmount);

				// Issuer part
				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse);
				Assert.Equal(numberOfCredentials, client.Credentials.Count());
				var issuedCredential = client.Credentials.First();
				Assert.True(issuedCredential.Amount.IsZero);
			}

			{
				var credentialRequest = client.AsCredentialRequestBuilder()
					.RequestCredentialFor(Money.Zero)
					.RequestCredentialFor(Money.Zero)
					.Build();

				Assert.True(credentialRequest.IsNullRequest);
				var requested = credentialRequest.Requested.ToArray();
				Assert.Equal(numberOfCredentials, requested.Count());
				Assert.Empty(requested[0].BitCommitments);
				Assert.Empty(requested[1].BitCommitments);
				Assert.Equal(Money.Zero, credentialRequest.DeltaAmount);

				// Issuer part
				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse);
				var credentials = client.Credentials.ToArray();
				Assert.NotEmpty(credentials);
				Assert.Equal(6, credentials.Count());
				Assert.All(credentials, x => Assert.True(x.Amount.IsZero));

				credentialRequest = client.AsCredentialRequestBuilder()
					.RequestCredentialFor(Money.Coins(1))
					.RequestCredentialFor(Money.Zero)
					.Build();

				Assert.False(credentialRequest.IsNullRequest);
				var credentialRequested = credentialRequest.Requested.ToArray();
				Assert.Equal(numberOfCredentials, credentialRequested.Count());
				Assert.NotEmpty(credentialRequested[0].BitCommitments);
				Assert.NotEmpty(credentialRequested[1].BitCommitments);

				credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse);
				credentials = client.Credentials.ToArray();
				Assert.NotEmpty(credentials);
				Assert.Equal(6, credentials.Count());
				Assert.True(credentials[0].Amount.IsZero);
				Assert.True(credentials[1].Amount.IsZero);
				Assert.True(credentials[2].Amount.IsZero);
				Assert.Equal(new Scalar(100_000_000), credentials[3].Amount);
				Assert.True(credentials[4].Amount.IsZero);
			}

			{
				var availableCredentials = client.Credentials.ToArray();

				var credentialRequest = client.AsCredentialRequestBuilder()
					.RequestCredentialFor(Money.Coins(0.5m))
					.RequestCredentialFor(Money.Coins(0.5m))
					.PresentCredentials(availableCredentials.Single(x => !x.Amount.IsZero))
					.Build();

				Assert.False(credentialRequest.IsNullRequest);
				var requested = credentialRequest.Requested.ToArray();
				Assert.Equal(numberOfCredentials, requested.Count());
				Assert.NotEmpty(requested[0].BitCommitments);
				Assert.NotEmpty(requested[1].BitCommitments);
				Assert.Equal(Money.Zero, credentialRequest.DeltaAmount); 

				// Issuer part
				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);

				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client.HandleResponse(credentialResponse);
				var credentials = client.Credentials.ToArray();
				Assert.NotEmpty(credentials);
				Assert.Equal(6, credentials.Count());

				var nonNullCredentials = client.Credentials.Where(x => !x.Amount.IsZero).ToArray();
				Assert.Equal(new Scalar(50_000_000), nonNullCredentials[0].Amount);
				Assert.Equal(new Scalar(50_000_000), nonNullCredentials[1].Amount);
			}

			{
				var client0 = new WabiSabiClient(sk.ComputeCoordinatorParameters(), numberOfCredentials, rnd);
				var credentialRequest = client0.AsCredentialRequestBuilder()
					.Build();

				var issuer = new CredentialIssuer(sk, numberOfCredentials, rnd);
				var credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse);

				credentialRequest = client0.AsCredentialRequestBuilder()
					.RequestCredentialFor(Money.Coins(1))
					.Build();

				credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse);

				var nonNullCredentials = client0.Credentials.Where(x => !x.Amount.IsZero).ToArray();

				credentialRequest = client0.AsCredentialRequestBuilder()
					.PresentCredentials(nonNullCredentials)
					.Build();

				// Issuer part
				credentialResponse = issuer.HandleRequest(credentialRequest);
				client0.HandleResponse(credentialResponse);
				var credentials = client0.Credentials.ToArray();
				Assert.NotEmpty(credentials);
				Assert.Equal(numberOfCredentials, credentials.Count());
			}
		}
	}
}