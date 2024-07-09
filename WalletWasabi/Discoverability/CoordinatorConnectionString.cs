using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Web;
using NBitcoin;

namespace WalletWasabi.Discoverability;

public record CoordinatorConnectionString(
	string Name,
	Network Network,
	Uri CoordinatorUri,
	decimal CoordinationFeeRate,
	int AbsoluteMinInputCount,
	Uri ReadMore)
{
	public override string ToString()
	{
		return string.Join("&",
		[
			$"name={Uri.EscapeDataString(Name)}",
			$"network={Network.Name}",
			$"coordinatorUri={Uri.EscapeDataString(CoordinatorUri.ToString())}",
			$"coordinationFeeRate={CoordinationFeeRate.ToString(CultureInfo.InvariantCulture)}",
			$"readMore={Uri.EscapeDataString(ReadMore.ToString())}",
			$"absoluteMinInputCount={AbsoluteMinInputCount.ToString()}",
		]);
	}

	public static bool TryParse(string s, [NotNullWhen(true)] out CoordinatorConnectionString? coordinatorConnectionString)
	{
		coordinatorConnectionString = null;

		var queryString = HttpUtility.ParseQueryString(s);
		string[] requiredParams = ["name", "network", "CoordinatorUri", "coordinationFeeRate", "absoluteMinInputCount", "readMore"];

		if (requiredParams.Any(param => string.IsNullOrEmpty(queryString[param])))
		{
			return false;
		}

		var name = queryString["name"]!;

		var network = Network.GetNetwork(queryString["network"]!);
		if (network is null)
		{
			return false;
		}

		if (!Uri.TryCreate(queryString["coordinatorUri"], UriKind.Absolute, out var coordinatorUri))
		{
			return false;
		}

		if (!decimal.TryParse(queryString["coordinationFeeRate"], NumberStyles.Any, CultureInfo.InvariantCulture, out var coordinationFeeRate))
		{
			return false;
		}

		if (!int.TryParse(queryString["absoluteMinInputCount"], out var absoluteMinInputCount))
		{
			return false;
		}

		if (!Uri.TryCreate(queryString["readMore"], UriKind.Absolute, out var readMore))
		{
			return false;
		}

		coordinatorConnectionString = new CoordinatorConnectionString(
			name,
			network,
			coordinatorUri,
			coordinationFeeRate,
			absoluteMinInputCount,
			readMore);

		return true;
	}
}
