using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierAuditArchiver
{
	public CoinVerifierAuditArchiver(string directoryPath)
	{
		IoHelpers.EnsureDirectoryExists(directoryPath);
		BaseDirectoryPath = directoryPath;
	}

	private string BaseDirectoryPath { get; }

	private AsyncLock FileAsyncLock { get; } = new();

	public async Task SaveAuditsAsync(IEnumerable<CoinVerifyResult> results, CancellationToken cancellationToken)
	{
		List<string> fileLines = new();

		foreach (CoinVerifyResult result in results)
		{
			string details = "No details";

			if (result.ApiResponseItem is not null)
			{
				var reportId = result.ApiResponseItem?.Report_info_section.Report_id;
				var reportType = result.ApiResponseItem?.Report_info_section.Report_type;
				var ids = result.ApiResponseItem?.Cscore_section.Cscore_info?.Select(x => x.Id) ?? Enumerable.Empty<int>();
				var categories = result.ApiResponseItem?.Cscore_section.Cscore_info.Select(x => x.Name) ?? Enumerable.Empty<string>();

				var detailsArray = new string[]
				{
					reportId ?? "ReportID None",
					reportType ?? "ReportType None",
					ids.Any() ? string.Join(' ', ids) : "FlagIds None",
					categories.Any() ? string.Join(' ', categories) : "Risk categories None"
				};

				details = ReplaceAndJoin(':', detailsArray, '-');
			}
			else if (result.Exception is not null)
			{
				details = result.Exception.Message;
			}

			var auditAsArray = new string[]
			{
				$"{DateTimeOffset.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
				$"{result.Coin.Outpoint}",
				$"{result.Coin.ScriptPubKey.GetDestinationAddress(Network.Main)}",
				$"{result.ShouldBan}",
				$"{result.ShouldRemove}",
				$"{result.Coin.Amount}",
				$"{result.Reason}",
				$"{details}"
			};

			var audit = ReplaceAndJoin(',', auditAsArray, '-');

			fileLines.Add(audit);
		}

		// Sanity check: if there is nothing to write, don't append the file with an empty line.
		if (fileLines.Count <= 0)
		{
			return;
		}

		var currentDate = DateTimeOffset.UtcNow;
		string fileName = $"VerifierAudits.{currentDate:yyyy.MM}.txt";
		string filePath = Path.Combine(BaseDirectoryPath, fileName);

		using (await FileAsyncLock.LockAsync(cancellationToken))
		{
			await File.AppendAllLinesAsync(filePath, fileLines, cancellationToken).ConfigureAwait(false);
		}
	}

	private string ReplaceAndJoin(char separator, IEnumerable<string> textArray, char replacment)
	{
		var cleanTextArray = textArray.Select(x => x.Replace(separator, replacment));
		return string.Join(separator, cleanTextArray);
	}
}
