using Newtonsoft.Json;
using WalletWasabi.Affiliation.Models.CoinJoinNotification;
using WalletWasabi.Affiliation.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class AffiliationSerializationTests
{
	[Fact]
	public void FeeRateSerialization()
	{
		AssertSerialization(new CoordinatorFeeRate(0.003m));
	}

	[Fact]
	public void AmbiguousFeeRateSerialization()
	{
		Assert.Throws<ArgumentException>(() => AssertSerialization(new CoordinatorFeeRate(1e-9m)));
	}

	private static void AssertSerialization<T>(T message)
	{
		var serializedMessage = JsonConvert.SerializeObject(message, AffiliationJsonSerializationOptions.Settings);
		var deserializedMessage = JsonConvert.DeserializeObject<T>(serializedMessage, AffiliationJsonSerializationOptions.Settings);
		var reserializedMessage = JsonConvert.SerializeObject(deserializedMessage, AffiliationJsonSerializationOptions.Settings);

		Assert.Equal(serializedMessage, reserializedMessage);
	}
}
