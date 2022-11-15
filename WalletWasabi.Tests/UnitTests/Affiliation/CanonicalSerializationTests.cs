using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class CanonicalSerializationTests
{
	[Fact]
	public void DelimitersSerialization()
	{
		var data = new { a = 1 };
		string json = JsonConvert.SerializeObject(data, CanonicalJsonSerializationOptions.Settings);
		Assert.Equal("{\"a\":1}", json);
	}

	[Fact]
	public void OrderSerialization()
	{
		var data = new { b = 1, a = 2 };
		string json = JsonConvert.SerializeObject(data, CanonicalJsonSerializationOptions.Settings);
		Assert.Equal("{\"a\":2,\"b\":1}", json);
	}
}
