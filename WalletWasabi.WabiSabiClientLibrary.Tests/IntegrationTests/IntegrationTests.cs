using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;

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
	public GetZeroCredentialRequestsTestVectors() : base("GetRealCredentialRequests.json", "get-real-credential-requests")
	{
	}
}

public class GetRealCredentialRequestsTestVectors : TestVectors
{
	public GetRealCredentialRequestsTestVectors() : base("GetZeroCredentialRequests.json", "get-zero-credential-requests")
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
