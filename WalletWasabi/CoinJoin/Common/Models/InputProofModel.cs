using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models;

public class InputProofModel
{
	[Required]
	[JsonConverter(typeof(OutPointAsTxoRefJsonConverter))]
	public OutPoint Input { get; set; }

	[Required]
	[JsonConverter(typeof(CompactSignatureJsonConverter))]
	public CompactSignature Proof { get; set; }
}
