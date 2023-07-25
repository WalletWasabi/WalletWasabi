using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using WabiSabi.Crypto;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Tests.IntegrationTests;

public class IntegrationsTest
{
	private readonly WebApplicationFactory<Program> _factory;

	public IntegrationsTest()
	{
		_factory = new();
	}

	[Theory]
	[ClassData(typeof(GetZeroCredentialRequestsTestVectors))]
	[ClassData(typeof(GetRealCredentialRequestsTestVectors))]
	[ClassData(typeof(GetCredentialsVectors))]
	[ClassData(typeof(GetOutpusAmountsTestVectors))]
	[ClassData(typeof(SelectInputsForRoundTestVectors))]
	[ClassData(typeof(GetAnonymityScoresTestVectors))]
	public async Task TestPostAsync([System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "xUnit1026")] string name, string method, string requestContentString, int expectedStatusCode, string expectedResponseContentString)
	{
		HttpClient client = _factory.CreateClient();

		using StringContent requestContent = new StringContent(requestContentString, Encoding.UTF8, "application/json");
		HttpResponseMessage response = await client.PostAsync(method, requestContent);

		Assert.Equal(expectedStatusCode, (int)response.StatusCode);

		string responseContentString = await response.Content.ReadAsStringAsync();

		Assert.Equal(expectedResponseContentString, responseContentString);
	}

	[Fact]
	public async Task TestCredentialSerialNumberUniquenessAsync()
	{
		HttpClient client = _factory.CreateClient();
		string requestContentString = """{ "credentialIssuerParameters": { "cw": "02BF822F22E5CF2A1725144FB6898EEBC9AD59AEA2C6267F6E9F819517E2B9B882", "i": "02720432E49A94D45794D76143914FCD9E7F9669BFD08B0EC8B45891E228D2D6E8" }, "maxCredentialValue": 255 }""";
		using StringContent requestContent = new StringContent(requestContentString, Encoding.UTF8, "application/json");

		HashSet<string> randomnessHashSet = new();
		foreach (var _ in Enumerable.Range(0, 10000))
		{
			HttpResponseMessage response = await client.PostAsync("get-zero-credential-requests", requestContent);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			string responseContentString = await response.Content.ReadAsStringAsync();
			bool isUnique = randomnessHashSet.Add(Regex.Match(responseContentString, "\"randomness\":\"([^\"]*)\"").Groups[1].Value);
			Assert.True(isUnique);
		}
	}

}

public class TestVectors : TheoryData<string, string, string, int, string>
{
	public TestVectors(string testVectorsFile, string methodName)
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string assemblyName = assembly.GetName().Name;
		string fileName = $"{assemblyName}.IntegrationTests.TestVectors.{testVectorsFile}";
		using Stream stream = assembly.GetManifestResourceStream(fileName);
		using StreamReader streamReader = new StreamReader(stream);
		IEnumerable<TestVector> testVectors = JsonConvert.DeserializeObject<IEnumerable<TestVector>>(streamReader.ReadToEnd());

		foreach (TestVector testVector in testVectors)
		{
			Add(testVector.Name, methodName, JsonConvert.SerializeObject(testVector.Request), testVector.ExpectedStatusCode, JsonConvert.SerializeObject(testVector.ExpectedResponse));
		}
	}

	private record TestVector(string Name, object Request, int ExpectedStatusCode, object ExpectedResponse);
}

public class GetZeroCredentialRequestsTestVectors : TestVectors
{
	public GetZeroCredentialRequestsTestVectors() : base("GetZeroCredentialRequests.json", "get-zero-credential-requests")
	{
	}
}

public class GetRealCredentialRequestsTestVectors : TestVectors
{
	public GetRealCredentialRequestsTestVectors() : base("GetRealCredentialRequests.json", "get-real-credential-requests")
	{
	}
}

public class GetCredentialsVectors : TestVectors
{
	public GetCredentialsVectors() : base("GetCredentials.json", "get-credentials")
	{
	}
}

public class GetOutpusAmountsTestVectors : TestVectors
{
	public GetOutpusAmountsTestVectors() : base("GetOutputsAmounts.json", "get-outputs-amounts")
	{
	}
}

public class SelectInputsForRoundTestVectors : TestVectors
{
	public SelectInputsForRoundTestVectors() : base("SelectInputsForRound.json", "select-inputs-for-round")
	{
	}
}

public class GetAnonymityScoresTestVectors : TestVectors
{
	public GetAnonymityScoresTestVectors() : base("GetAnonymityScores.json", "get-anonymity-scores")
	{
	}
}
