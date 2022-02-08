using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class CoinJoinTransactionArchiver
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true, // Pretty print.
	};

	public CoinJoinTransactionArchiver(string directoryPath)
	{
		BaseStoragePath = directoryPath;
	}

	/// <summary>Base path where to store transactions.</summary>
	private string BaseStoragePath { get; }

	/// <summary>
	/// Save CoinJoin transaction in a JSON file for later inspection.
	/// </summary>
	/// <param name="transaction">Transaction to store.</param>
	/// <param name="currentDate">Date when the transaction was created or <c>null</c> to use current UTC time.</param>
	/// <returns>Full path to the stored file.</returns>
	public async Task<string> StoreJsonAsync(Transaction transaction, FeeRate feeRate, DateTimeOffset? currentDate = null)
	{
		currentDate ??= DateTimeOffset.UtcNow;

		long unixTimeStampMs = currentDate.Value.ToUnixTimeMilliseconds();
		string txBytes = transaction.ToHex();

		// Serialize entry.
		TransactionInfo entry = new(unixTimeStampMs, feeRate.SatoshiPerByte, transaction.GetHash().ToString(), txBytes);
		string json = JsonSerializer.Serialize(entry, Options);

		// Use a date format in the file name to let the files be sorted by date by default.
		string fileName = $"tx.{currentDate.Value:yyyy.MM.dd-HH-mm-ss}.{transaction.GetHash().ToString()[..5]}.json";
		string folderPath = Path.Combine(BaseStoragePath, $"{currentDate.Value:yyyy-MM}");
		IoHelpers.EnsureDirectoryExists(folderPath);

		string filePath = Path.Combine(folderPath, fileName);
		await File.WriteAllTextAsync(filePath, json, CancellationToken.None).ConfigureAwait(false);

		return filePath;
	}

	public async IAsyncEnumerable<TransactionInfo> ReadJsonAsync(DateTimeOffset from, DateTimeOffset to, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (to < from)
		{
			throw new ArgumentException("Invalid time period", nameof(from));
		}

		List<string> dirsToRead = new();
		int monthOffset = 0;

		do
		{
			DateTimeOffset date = new(from.Year, from.Month + monthOffset, from.Day, 0, 0, 0, from.Offset);
			if (date > to)
			{
				break;
			}
			dirsToRead.Add(Path.Combine(BaseStoragePath, $"{date:yyyy-MM}"));
			monthOffset++;

			cancellationToken.ThrowIfCancellationRequested();
		}
		while (true);

		foreach (var montlyCoinJoinDirectory in dirsToRead.Select(d => new DirectoryInfo(d)))
		{
			foreach (var file in montlyCoinJoinDirectory.GetFiles("*.json"))
			{
				var json = await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false);
				TransactionInfo? ti = JsonSerializer.Deserialize<TransactionInfo>(json, Options);
				if (ti is null)
				{
					continue;
				}

				var txTime = DateTimeOffset.FromUnixTimeSeconds(ti.Created);
				if (txTime < from && txTime > to)
				{
					continue;
				}

				yield return ti;
			}
		}
	}

	/// <summary>
	/// Represents a single CoinJoin transaction.
	/// </summary>
	/// <param name="Created">UNIX timestamp in milliseconds when the transaction was created.</param>
	/// <param name="TxHash">Hash of the transaction.</param>
	/// <param name="RawTransaction">Raw bitcoin transaction in hexadecimal representation.</param>
	public record TransactionInfo(
		[property: JsonPropertyName("createdMs")] long Created,
		[property: JsonPropertyName("feeRate")] decimal FeeRate,
		[property: JsonPropertyName("txHash")] string TxHash,
		[property: JsonPropertyName("txRawHex")] string RawTransaction
	);
}
