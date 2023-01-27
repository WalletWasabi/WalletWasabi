using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierAuditArchiver : IAsyncDisposable
{
	public CoinVerifierAuditArchiver(string directoryPath)
	{
		IoHelpers.EnsureDirectoryExists(directoryPath);
		BaseDirectoryPath = directoryPath;
	}

	private string BaseDirectoryPath { get; }

	private AsyncLock FileAsyncLock { get; } = new();

	private object LogLinesLock { get; } = new();

	private List<AuditLine> LogLines { get; } = new();

	public void LogException(uint256 roundId, Exception exception)
	{
		var logArray = new string[]
		{
			$"{roundId}",
			$"{exception.Message}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.Round, logArray);
	}

	public void LogRoundEvent(uint256 roundId, string message)
	{
		var logAsArray = new string[]
		{
			$"{message}",
			$"Round ID: {roundId}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.Round, logAsArray);
	}

	public void LogVerificationResult(CoinVerifyResult coinVerifyResult, Reason reason, ApiResponseItem? apiResponseItem = null, Exception? exception = null)
	{
		string details = "No details";

		if (apiResponseItem is not null)
		{
			var reportId = apiResponseItem?.Report_info_section.Report_id;
			var reportType = apiResponseItem?.Report_info_section.Report_type;
			var ids = apiResponseItem?.Cscore_section.Cscore_info?.Select(x => x.Id) ?? Enumerable.Empty<int>();
			var categories = apiResponseItem?.Cscore_section.Cscore_info.Select(x => x.Name) ?? Enumerable.Empty<string>();

			var detailsArray = new string[]
			{
					reportId ?? "ReportID None",
					reportType ?? "ReportType None",
					ids.Any() ? string.Join(' ', ids) : "FlagIds None",
					categories.Any() ? string.Join(' ', categories) : "Risk categories None"
			};

			var formattedLines = detailsArray.Select(line => line.Replace(':', '-'));
			details = string.Join(",", formattedLines);
		}
		else if (exception is not null)
		{
			details = exception.Message;
		}

		var auditAsArray = new string[]
		{
				$"{coinVerifyResult.Coin.Outpoint}",
				$"{coinVerifyResult.Coin.ScriptPubKey.GetDestinationAddress(Network.Main)}",
				$"{coinVerifyResult.ShouldBan}",
				$"{coinVerifyResult.ShouldRemove}",
				$"{coinVerifyResult.Coin.Amount}",
				$"{reason}",
				$"{details}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.VerificationResult, auditAsArray);
	}

	public async Task SaveAuditsAsync()
	{
		AuditLine[] logLines;

		lock (LogLinesLock)
		{
			logLines = LogLines.ToArray();
			LogLines.Clear();
		}

		if (logLines.Length <= 0)
		{
			return;
		}

		var firstDate = logLines.Select(x => x.DateTimeOffset).First();
		string fileName = $"VerifierAudits.{firstDate:yyyy.MM}.txt";
		string filePath = Path.Combine(BaseDirectoryPath, fileName);

		List<string> lines = new();

		foreach (AuditLine line in logLines)
		{
			var logAsArray = new string[]
			{
				$"{line.DateTimeOffset:yyyy-MM-dd HH:mm:ss}",
				$"{line.AuditEventType}",
				$"{line.LogMessage}"
			};

			var text = string.Join(',', logAsArray);
			lines.Add(text);
		}

		using (await FileAsyncLock.LockAsync(CancellationToken.None))
		{
			await File.AppendAllLinesAsync(filePath, lines, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private void AddLogLineAndFormatCsv(DateTimeOffset dateTime, AuditEventType auditEventType, IEnumerable<string> unformattedTexts)
	{
		var csvCompatibleTexts = unformattedTexts.Select(text => text.Replace(',', ' '));
		var csvCompatibleLogMessage = string.Join(',', csvCompatibleTexts);

		lock (LogLinesLock)
		{
			LogLines.Add(new AuditLine(dateTime, auditEventType, csvCompatibleLogMessage));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await SaveAuditsAsync().ConfigureAwait(false);
	}

	public record AuditLine(DateTimeOffset DateTimeOffset, AuditEventType AuditEventType, string LogMessage);
}
