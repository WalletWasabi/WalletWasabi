using NBitcoin;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

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
	/// Save coinjoin transaction in a JSON file for later inspection.
	/// </summary>
	/// <param name="transaction">Transaction to store.</param>
	/// <param name="currentDate">Date when the transaction was created or <c>null</c> to use current UTC time.</param>
	/// <returns>Full path to the stored file.</returns>
	public async Task<string> StoreJsonAsync(Transaction transaction, DateTimeOffset? currentDate = null)
	{
		currentDate ??= DateTimeOffset.UtcNow;

		long unixTimeStampMs = currentDate.Value.ToUnixTimeMilliseconds();
		string txBytes = transaction.ToHex();

		// Serialize entry.
		TransactionInfo entry = new(unixTimeStampMs, transaction.GetHash().ToString(), txBytes);
		string json = JsonSerializer.Serialize(entry, Options);

		// Use a date format in the file name to let the files be sorted by date by default.
		string fileName = $"tx.{currentDate.Value:yyyy.MM.dd-HH-mm-ss}.{transaction.GetHash().ToString()[..5]}.json";
		string folderPath = Path.Combine(BaseStoragePath, $"{currentDate.Value:yyyy-MM}");
		IoHelpers.EnsureDirectoryExists(folderPath);

		string filePath = Path.Combine(folderPath, fileName);
		await File.WriteAllTextAsync(filePath, json, CancellationToken.None).ConfigureAwait(false);

		return filePath;
	}

	/// <summary>
	/// Represents a single coinjoin transaction.
	/// </summary>
	/// <param name="Created">UNIX timestamp in milliseconds when the transaction was created.</param>
	/// <param name="TxHash">Hash of the transaction.</param>
	/// <param name="RawTransaction">Raw bitcoin transaction in hexadecimal representation.</param>
	public record TransactionInfo(
		[property: JsonPropertyName("createdMs")] long Created,
		[property: JsonPropertyName("txHash")] string TxHash,
		[property: JsonPropertyName("txRawHex")] string RawTransaction
	);
}
