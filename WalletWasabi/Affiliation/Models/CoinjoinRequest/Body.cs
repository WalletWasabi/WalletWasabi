using System.Collections.Generic;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;
using System.Text;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Body
{
	public Body(IEnumerable<Input> inputs, IEnumerable<Output> outputs, long slip44CoinType, decimal feeRate, long noFeeThreshold, long minRegistrableAmount, long timestamp)
	{
		Inputs = inputs;
		Outputs = outputs;
		Slip44CoinType = slip44CoinType;
		FeeRate = feeRate;
		NoFeeThreshold = noFeeThreshold;
		MinRegistrableAmount = minRegistrableAmount;
		Timestamp = timestamp;
	}

	[JsonProperty(PropertyName = "inputs")]
	public IEnumerable<Input> Inputs { get; }

	[JsonProperty(PropertyName = "outputs")]
	public IEnumerable<Output> Outputs { get; }

	[JsonProperty(PropertyName = "slip44_coin_type")]
	public long Slip44CoinType { get; }

	[JsonProperty(PropertyName = "fee_rate")]
	[JsonConverter(typeof(FeeRateJsonConverter))]
	public decimal FeeRate { get; }

	[JsonProperty(PropertyName = "no_fee_threshold")]
	public long NoFeeThreshold { get; }

	[JsonProperty(PropertyName = "min_registrable_amount")]
	public long MinRegistrableAmount { get; }

	[JsonProperty(PropertyName = "timestamp")]
	public long Timestamp { get; }
}
