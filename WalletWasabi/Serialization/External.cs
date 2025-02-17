using System.Linq;
using WalletWasabi.Backend.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Serialization;

public static partial class Decode
{
	public static readonly Decoder<RelativeCpfpInfo> RelativeCpfpInfo =
		Object(get => new RelativeCpfpInfo(
			get.Required("TxId", UInt256),
			get.Required("Fee", Int64),
			get.Required("Weight", Int64)
		));

	public static readonly Decoder<CpfpInfo> CpfpInfo =
		Object(get => new CpfpInfo(
			get.Required("Ancestors", Array(RelativeCpfpInfo)).ToList(),
			get.Required("Fee", Decimal),
			get.Required("EffectiveFeePerVSize", Decimal),
			get.Required("AdjustedVSize", Decimal)
		));

	public static readonly Decoder<ExchangeRate> BitstampExchangeRate =
		Object(get => new ExchangeRate{
			Ticker = "USD",
			Rate = get.Required("bid", Decimal)
		});

	public static readonly Decoder<ExchangeRate> GeminiExchangeRateInfo =
		Object(get => new ExchangeRate{
			Ticker = "USD",
			Rate = get.Required("Bid", Decimal)
		});

	public static readonly Decoder<ExchangeRate> CoinGeckoExchangeRate =
		Object(get => new ExchangeRate{
			Ticker = "USD",
			Rate = get.Required("current_price", Decimal)
		});

	public static readonly Decoder<ExchangeRate> CoinbaseExchangeRate =
		Object(get => new ExchangeRate{
			Ticker = "USD",
			Rate = get.Required("Data", Field("Rates", Field("USD", Decimal)))
		});

	public static readonly Decoder<ExchangeRate> BlockchainInfoExchangeRates =
		Object(get => new ExchangeRate{
			Ticker = "USD",
			Rate = get.Required("USD", Field("Sell", Decimal))
		});
}
