using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Extensions;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

/// <summary>
/// Converter used to convert <see cref="SendWorkflow"/> to and from JSON.
/// </summary>
/// <seealso cref="JsonConverter" />
public class SendWorkflowJsonConverter : JsonConverter<SendWorkflow>
{
	/// <inheritdoc />
	public override SendWorkflow ReadJson(JsonReader reader, Type objectType, SendWorkflow existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var sendWorkflowString = ((string?)reader.Value)?.Trim();

		if (sendWorkflowString is null)
		{
			throw new ArgumentNullException(nameof(sendWorkflowString));
		}

		if (Enum.TryParse(sendWorkflowString, true, out SendWorkflow result))
		{
			return result;
		}

		throw new JsonSerializationException($"Invalid Send Workflow: {sendWorkflowString}");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, SendWorkflow value, JsonSerializer serializer)
	{
		string sendWorkflow = value.FriendlyName()
		                          ?? throw new ArgumentNullException(nameof(value));

		writer.WriteValue(sendWorkflow);
	}
}
