using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.JsonConverters.Collections;
using WalletWasabi.JsonConverters.Timing;
using WalletWasabi.WabiSabi.Crypto.Serialization;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class JsonSerializationOptions
{
	private static readonly JsonSerializerSettings CurrentSettings = new()
	{
		Converters = new List<JsonConverter>()
			{
				new ScalarJsonConverter(),
				new GroupElementJsonConverter(),
				new OutPointJsonConverter(),
				new WitScriptJsonConverter(),
				new ScriptJsonConverter(),
				new OwnershipProofJsonConverter(),
				new NetworkJsonConverter(),
				new FeeRateJsonConverter(),
				new MoneySatoshiJsonConverter(),
				new Uint256JsonConverter(),
				new HashSetUint256JsonConverter(),
				new MultipartyTransactionStateJsonConverter(),
				new ExceptionDataJsonConverter(),
				new ExtPubKeyJsonConverter(),
				new TimeSpanJsonConverter(),
				new CoinJsonConverter(),
				new CoinJoinEventJsonConverter(),
				new GroupElementVectorJsonConverter(),
				new ScalarVectorJsonConverter(),
				new IssuanceRequestJsonConverter(),
				new CredentialPresentationJsonConverter(),
				new ProofJsonConverter(),
				new MacJsonConverter()
			}
	};

	public static readonly JsonSerializationOptions Default = new();

	private JsonSerializationOptions()
	{
	}

	public JsonSerializerSettings Settings => CurrentSettings;
}
