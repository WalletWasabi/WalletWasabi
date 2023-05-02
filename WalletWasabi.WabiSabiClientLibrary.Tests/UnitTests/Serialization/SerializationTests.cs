using Newtonsoft.Json;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;
using WalletWasabi.WabiSabiClientLibrary.Crypto;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Tests.UnitTests.Serialization;

public class SerializationTests
{
	[Fact]
	public void CredentialsResponseValidationSerialization()
	{
		DeterministicRandom random = new(0);
		CredentialIssuerSecretKey credentialIssuerSecretKey = new(random);
		GetZeroCredentialRequestsRequest getZeroCredentialRequestRequest = new(ProtocolConstants.MaxAmountPerAlice, credentialIssuerSecretKey.ComputeCredentialIssuerParameters());
		GetZeroCredentialRequestsResponse getZeroCredentialRequestResponse = CredentialHelper.GetZeroCredentialRequests(getZeroCredentialRequestRequest, random);
		CredentialsResponseValidation credentialsResponseValidation = getZeroCredentialRequestResponse.ZeroCredentialRequests.CredentialsResponseValidation;

		AssertSerialization(credentialsResponseValidation);
	}

	private void AssertSerialization<TModel>(TModel model)
	{
		string modelSerialized = JsonConvert.SerializeObject(model, JsonSerializationOptions.Default.Settings);
		Console.WriteLine(modelSerialized);
		TModel deserializedModel = JsonConvert.DeserializeObject<TModel>(modelSerialized, JsonSerializationOptions.Default.Settings);
		string modelReserialized = JsonConvert.SerializeObject(deserializedModel, JsonSerializationOptions.Default.Settings);
		Assert.Equal(modelSerialized, modelSerialized);
	}
}
