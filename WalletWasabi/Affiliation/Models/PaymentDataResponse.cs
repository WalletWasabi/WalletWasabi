using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Models;

public class PaymentDataResponse
{
	[JsonProperty(PropertyName = "payment_data")]
	public byte[] PaymentData;

	public PaymentDataResponse(byte[] paymentData)
	{
		PaymentData = paymentData;
	}
}
