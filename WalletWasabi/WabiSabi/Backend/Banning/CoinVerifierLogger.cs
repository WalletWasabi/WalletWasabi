using AsyncLock = AsyncKeyedLock.AsyncNonKeyedLocker;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierLogger : IAsyncDisposable
{
	private const char CsvSeparator = ',';

	public CoinVerifierLogger(string directoryPath)
	{
		IoHelpers.EnsureDirectoryExists(directoryPath);
		DirectoryPath = directoryPath;
	}

	private string DirectoryPath { get; }

	private AsyncLock FileAsyncLock { get; } = new();

	private object LogLinesLock { get; } = new();

	private List<AuditEvent> LogLines { get; } = new();

	public void LogException(uint256 roundId, Exception exception)
	{
		var logArray = new string[]
		{
			$"{roundId}",
			$"{exception.Message}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.Exception, logArray);
	}

	public void LogRoundEvent(uint256 roundId, string message)
	{
		var logAsArray = new string[]
		{
			$"Round ID: {roundId}",
			$"{message}"
		};

		AddLogLineAndFormatCsv(DateTimeOffset.UtcNow, AuditEventType.Round, logAsArray);
	}

	public void LogVerificationResult(CoinVerifyResult coinVerifyResult, Reason reason, ApiResponseItem? apiResponseItem = null, Exception? exception = null)
	{
		string details = "No details";

		if (apiResponseItem is not null)
		{
			var reportId = apiResponseItem?.Report_info_section.Report_id;
			var reportHeight = apiResponseItem?.Report_info_section.Report_block_height.ToString();
			var reportType = apiResponseItem?.Report_info_section.Report_type;
			var ids = apiResponseItem?.Cscore_section.Cscore_info?.Select(x => x.Id) ?? Enumerable.Empty<int>();
			var categories = apiResponseItem?.Cscore_section.Cscore_info.Select(x => x.Name) ?? Enumerable.Empty<string>();
			var addressUsed = apiResponseItem?.Report_info_section.Address_used ?? false;

			var detailsArray = new string[]
			{
					reportId ?? "ReportID None",
					reportHeight ?? "ReportHeight None",
					reportType ?? "ReportType None",
					addressUsed ? "Address used" : "Address not used",
					ids.Any() ? string.Join(' ', ids) : "FlagIds None",
					categories.Any() ? string.Join(' ', categories) : "Risk categories None"
			};

			// Separate the different values of the ApiResponseItem with '|', so the details will be one value in the CSV file.
			details = string.Join("|", detailsArray);
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
		AuditEvent[] auditLines;

		lock (LogLinesLock)
		{
			auditLines = LogLines.ToArray();
			LogLines.Clear();
		}

		if (auditLines.Length == 0)
		{
			return;
		}

		List<string> lines = new();

		foreach (AuditEvent line in auditLines)
		{
			var auditParts = new string[]
			{
				$"{line.DateTimeOffset:yyyy-MM-dd HH:mm:ss}",
				$"{line.AuditEventType}",
				$"{line.LogMessage}"
			};

			var audit = string.Join(CsvSeparator, auditParts);
			lines.Add(audit);
		}

		var firstDate = auditLines.Select(x => x.DateTimeOffset).First();
		string filePath = Path.Combine(DirectoryPath, $"VerifierAudits.{firstDate:yyyy.MM}.txt");

		using (await FileAsyncLock.LockAsync(CancellationToken.None))
		{
			await File.AppendAllLinesAsync(filePath, lines, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private void AddLogLineAndFormatCsv(DateTimeOffset dateTime, AuditEventType auditEventType, IEnumerable<string> unformattedTexts)
	{
		var csvCompatibleTexts = unformattedTexts.Select(text => text.Replace(CsvSeparator, ' '));
		var csvLine = string.Join(CsvSeparator, csvCompatibleTexts);

		lock (LogLinesLock)
		{
			LogLines.Add(new AuditEvent(dateTime, auditEventType, csvLine));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await SaveAuditsAsync().ConfigureAwait(false);
	}

	public record AuditEvent(DateTimeOffset DateTimeOffset, AuditEventType AuditEventType, string LogMessage);
}
