using System.Text;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Payload(Header Header, Body Body)
{
	public byte[] GetCanonicalSerialization() =>
		Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(this, CanonicalJsonSerializationOptions.Settings));
}
