using NBitcoin;
using NBitcoin.Crypto;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control;

namespace WalletWasabi.Services;

public static class WasabiSignerTools
{
	private const string WasabiKeyHeadline = "WASABI PRIVATE KEY";
	private const string PGPMessageHeadline = "PGP SIGNED MESSAGE";
	private const string PGPSignatureHeadline = "PGP SIGNATURE";
	private const string WasabiSignatureHeadline = "WASABI SIGNATURE";
	public static string ShaSumsFileName { get; } = "SHA256SUMS.asc";

	public static Key GeneratePrivateKey() => new();

	public static bool TryGetPrivateKeyFromFile(string fileName, [NotNullWhen(true)] out Key? key)
	{
		key = null;
		try
		{
			string[] keyFileContent = File.ReadAllLines(fileName);
			bool isHeadlineBeginValid = keyFileContent[0] == $"-----BEGIN {WasabiKeyHeadline}-----";
			bool isHeadlineEndValid = keyFileContent[2] == $"-----END {WasabiKeyHeadline}-----";

			if (!isHeadlineBeginValid || !isHeadlineEndValid)
			{
				throw new ArgumentException("Wasabi private key file's content was invalid.");
			}

			string wif = keyFileContent[1];
			BitcoinSecret secret = new(wif, Network.Main);

			key = secret.PrivateKey;
			return true;
		}
		catch (Exception exc)
		{
			Logger.LogError("There was an error while reading Key from file.", exc);
		}
		return false;
	}

	private static bool VerifyShaSumsFile(string shaSumsFileContent, string base64Signature, PubKey publicKey)
	{
		try
		{
			uint256 hash = GenerateHashFromString(shaSumsFileContent);

			byte[] sigBytes = Convert.FromBase64String(base64Signature);
			var wasabiSignature = ECDSASignature.FromDER(sigBytes);
			return publicKey.Verify(hash, wasabiSignature);
		}
		catch (FormatException)
		{
			Logger.LogWarning($"{ShaSumsFileName}'s signature was invalid, DER bytes are not in right format.");
		}
		catch (Exception exc)
		{
			Logger.LogError(exc);
		}
		return false;
	}

	private static async Task<string[]> ReadShaSumsFileLinesAsync(string shaSumsFilePath, PubKey publicKey)
	{
		StringBuilder contentBuilder = new();
		string base64Signature = "";

		var sumsFileLines = await File.ReadAllLinesAsync(shaSumsFilePath).ConfigureAwait(false);

		string? headline = sumsFileLines.FirstOrDefault();
		int contentEndIndex = Array.IndexOf(sumsFileLines, $"-----END {PGPSignatureHeadline}-----");
		if (headline is null ||
			headline != $"-----BEGIN {PGPMessageHeadline}-----" ||
			contentEndIndex == -1)
		{
			throw new ArgumentException($"{ShaSumsFileName}'s content was invalid.");
		}

		for (int i = 0; i <= contentEndIndex; i++)
		{
			contentBuilder.AppendLine(sumsFileLines[i]);
		}

		int signatureBeginIndex = Array.IndexOf(sumsFileLines, $"-----BEGIN {WasabiSignatureHeadline}-----");
		if (signatureBeginIndex != -1)
		{
			base64Signature = sumsFileLines[signatureBeginIndex + 1].Trim();
		}

		bool isSignatureValid = VerifyShaSumsFile(contentBuilder.ToString().ReplaceLineEndings("\n"), base64Signature, publicKey);
		if (!isSignatureValid)
		{
			throw new ArgumentException($"Couldn't verify Wasabi's signature in {ShaSumsFileName}.");
		}
		return sumsFileLines;
	}

	public static async Task<uint256> GetAndVerifyInstallerFromShaSumsFileAsync(string shaSumsFilePath, string installerName, PubKey publicKey)
	{
		string[] sumsFileLines = await ReadShaSumsFileLinesAsync(shaSumsFilePath, publicKey).ConfigureAwait(false);

		int installerFilesEndIndex = Array.IndexOf(sumsFileLines, $"-----BEGIN {PGPSignatureHeadline}-----");
		for (int i = 1; i < installerFilesEndIndex; i++)
		{
			string line = sumsFileLines[i].Trim();
			if (string.IsNullOrEmpty(line))
			{
				continue;
			}
			string[] splitLine = line.Split(" ");
			string? filename = splitLine[1];
			if (filename == installerName)
			{
				return uint256.TryParse(splitLine[0], out uint256 installerHash) ?
					installerHash : throw new ArgumentNullException($"{filename}'s hash was invalid");
			}
		}
		throw new ArgumentException($"{installerName} can't be found.");
	}

	public static async Task<uint256> GenerateHashFromFileAsync(string filePath)
	{
		if (File.Exists(filePath))
		{
			byte[] bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
			using SHA256 sha = SHA256.Create();
			byte[] computedHash = sha.ComputeHash(bytes);
			return new(computedHash);
		}
		else
		{
			throw new FileNotFoundException($"Couldn't find file at {filePath}.");
		}
	}

	private static uint256 GenerateHashFromString(string content)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(content);
		using SHA256 sha = SHA256.Create();
		byte[] computedHash = sha.ComputeHash(bytes);
		return new(computedHash);
	}

	public static async Task SignAndSaveShaSumsFileAsync(string signatureFilePath, string destinationPath, Key key)
	{
		string content = await File.ReadAllTextAsync(signatureFilePath).ConfigureAwait(false);
		uint256 contentHash = GenerateHashFromString(content);

		ECDSASignature signature = key.Sign(contentHash);
		string base64Signature = Convert.ToBase64String(signature.ToDER());

		StringBuilder stringBuilder = new(content);
		stringBuilder.AppendLine($"-----BEGIN {WasabiSignatureHeadline}-----");
		stringBuilder.AppendLine(base64Signature);
		stringBuilder.AppendLine($"-----END {WasabiSignatureHeadline}-----");

		await File.WriteAllTextAsync(destinationPath, stringBuilder.ToString()).ConfigureAwait(false);
	}

	public static async Task SavePrivateKeyToFileAsync(string destinationPath, Key key)
	{
		try
		{
			if (File.Exists(destinationPath))
			{
				throw new ArgumentException("Private key file already exists.");
			}
			using StreamWriter streamWriter = new(destinationPath);
			await streamWriter.WriteLineAsync($"-----BEGIN {WasabiKeyHeadline}-----").ConfigureAwait(false);
			await streamWriter.WriteLineAsync(key.ToString(Network.Main)).ConfigureAwait(false);
			await streamWriter.WriteLineAsync($"-----END {WasabiKeyHeadline}-----").ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			Logger.LogError(exc);
			throw;
		}
	}
}
