using Newtonsoft.Json;
using WalletWasabi.Affiliation;
using WalletWasabi.Affiliation.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class SerializationTests
{
	[Fact]
	public void AffiliationFlagSerialization()
	{
		AssertSerialization(AffiliationFlag.Default);
		AssertSerialization(AffiliationFlag.Trezor);
	}

	private static void AssertSerialization<T>(T message)
	{
		var serializedMessage = JsonConvert.SerializeObject(message, JsonSerializationOptions.Settings);
		var deserializedMessage = JsonConvert.DeserializeObject<T>(serializedMessage, JsonSerializationOptions.Settings);
		var reserializedMessage = JsonConvert.SerializeObject(deserializedMessage, JsonSerializationOptions.Settings);

		Assert.Equal(reserializedMessage, serializedMessage);
	}
}
