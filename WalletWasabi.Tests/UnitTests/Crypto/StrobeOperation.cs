using Newtonsoft.Json;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class StrobeOperation
{
	[JsonConstructor]
	public StrobeOperation(string name, int security, string customString, uint inputLength, string output, bool isMeta, string inputData, string stateAfter, bool isStream)
	{
		Name = name;
		Security = security;
		CustomString = customString;
		InputLength = inputLength;
		Output = output;
		IsMeta = isMeta;
		InputData = inputData;
		StateAfter = stateAfter;
		IsStream = isStream;
	}

	[JsonProperty(PropertyName = "name")]
	public string Name { get; }

	[JsonProperty(PropertyName = "security")]
	public int Security { get; }

	[JsonProperty(PropertyName = "custom_string")]
	public string CustomString { get; }

	[JsonProperty(PropertyName = "input_length")]
	public uint InputLength { get; }

	[JsonProperty(PropertyName = "output")]
	public string Output { get; }

	[JsonProperty(PropertyName = "meta")]
	public bool IsMeta { get; }

	[JsonProperty(PropertyName = "input_data")]
	public string InputData { get; }

	[JsonProperty(PropertyName = "state_after")]
	public string StateAfter { get; }

	[JsonProperty(PropertyName = "stream")]
	public bool IsStream { get; }
}
