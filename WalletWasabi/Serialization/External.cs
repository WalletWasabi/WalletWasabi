using System.Linq;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Models;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Serialization;

public static partial class Decode
{
	public static readonly Decoder<RelativeCpfpInfo> RelativeCpfpInfo =
		Object(get => new RelativeCpfpInfo(
			get.Required("txid", UInt256),
			get.Required("fee", Int64),
			get.Required("weight", Int64)
		));

	public static readonly Decoder<CpfpInfo> CpfpInfo =
		Object(get => new CpfpInfo(
			get.Required("ancestors", Array(RelativeCpfpInfo)).ToList(),
			get.Required("fee", Decimal),
			get.Required("effectiveFeePerVsize", Decimal),
			get.Required("adjustedVsize", Decimal)
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

	public static readonly Decoder<TorIssue> TorIssueDecoder =
		Object(get => new TorIssue(
			get.Required("title", String),
			get.Required("resolved", Bool),
			get.Required("affected", Array(String)).ToList()));

	public static readonly Decoder<SystemItem> TorSystemItemDecoder =
		Object(get => new SystemItem(
			get.Required("name", String), get.Required("status", String),
			get.Required("unresolvedIssues", Array(TorIssueDecoder)).ToList()));

	public static readonly Decoder<TorNetworkStatus> TorStatus =
		Object(get => new TorNetworkStatus(get.Required("systems", Array(TorSystemItemDecoder)).ToList()));
}
