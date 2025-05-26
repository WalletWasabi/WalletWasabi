using System.Linq;
using WalletWasabi.Backend.Models;
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
		Object(get => new TorIssue
		{
			Is = get.Required("is", String),
			Title = get.Required("title", String),
			CreatedAt = get.Required("createdAt", String),
			LastMod = get.Required("lastMod", String),
			Permalink = get.Required("permalink", String),
			Severity = get.Required("severity", String),
			Resolved = get.Required("resolved", Bool),
			Informational = get.Required("informational", Bool),
			ResolvedAt = get.Required("resolvedAt", String),
			Affected = get.Required("affected", Array(String)).ToList(),
			Filename = get.Required("filename", String)
		});

	public static readonly Decoder<SystemItem> TorSystemItemDecoder =
		Object(get => new SystemItem
		{
			Name = get.Required("name", String),
			Description = get.Required("description", String),
			Category = get.Required("category", String),
			Status = get.Required("status", String),
			UnresolvedIssues = get.Required("unresolvedIssues", Array(TorIssueDecoder)).ToList()
		});

	public static readonly Decoder<TorNetworkStatus> TorStatus =
		Object(get => new TorNetworkStatus
		{
			Systems = get.Required("systems", Array(TorSystemItemDecoder)).ToList()
		});
}
