using System.Collections.Generic;
using System.Collections.Immutable;
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

	[Fact]
	public void AffiliateServersSerialization()
	{
		AssertSerialization(new Dictionary<AffiliationFlag, string> { { AffiliationFlag.Trezor, "www.test.io" } }.ToImmutableDictionary());
		AssertSerialization(new Dictionary<AffiliationFlag, string> { }.ToImmutableDictionary());
	}

	private static void AssertSerialization<T>(T message)
	{
		var serializedMessage = JsonConvert.SerializeObject(message, AffiliationJsonSerializationOptions.Settings);
		var deserializedMessage = JsonConvert.DeserializeObject<T>(serializedMessage, AffiliationJsonSerializationOptions.Settings);
		var reserializedMessage = JsonConvert.SerializeObject(deserializedMessage, AffiliationJsonSerializationOptions.Settings);

		Assert.Equal(reserializedMessage, serializedMessage);
	}
}
