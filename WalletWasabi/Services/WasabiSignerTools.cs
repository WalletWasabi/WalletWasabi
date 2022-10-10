using NBitcoin;
using NBitcoin.Crypto;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Logging;

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

	public static (string content, Dictionary<string, uint256> installerDictionary, string base64Signature) ReadShaSumsContent(string shaSumsFilePath, PubKey pubKey)
	{
		Dictionary<string, uint256> installerDictionary = new();
		string base64Signature = "";
		StringBuilder contentBuilder = new();

		string content = File.ReadAllText(shaSumsFilePath).Replace("\r\n", "\n");
		string[] sumsFileLines = content.Split("\n");

		string? headline = sumsFileLines.FirstOrDefault();
		int contentEndIndex = Array.IndexOf(sumsFileLines, $"-----END {PGPSignatureHeadline}-----");
		if (headline is null ||
			headline != $"-----BEGIN {PGPMessageHeadline}-----" ||
			contentEndIndex == -1)
		{
			throw new ArgumentException($"{ShaSumsFileName}'s content was invalid.");
		}
		int installerFilesEndIndex = Array.IndexOf(sumsFileLines, $"-----BEGIN {PGPSignatureHeadline}-----");
		for (int i = 0; i < installerFilesEndIndex; i++)
		{
			string line = sumsFileLines[i].Trim();
			contentBuilder.AppendLine(line);

			string[] splitLine = line.Split(" ");
			bool isHashValid = uint256.TryParse(splitLine[0], out uint256 installerHash);
			if (isHashValid)
			{
				string installerName = splitLine[1];
				installerDictionary.Add(installerName, installerHash);
			}
		}

		int signatureBeginIndex = Array.IndexOf(sumsFileLines, $"-----BEGIN {WasabiSignatureHeadline}-----");
		if (signatureBeginIndex != -1)
		{
			base64Signature = sumsFileLines[signatureBeginIndex + 1].Trim();
		}

		for (int i = installerFilesEndIndex; i < signatureBeginIndex; i++)
		{
			contentBuilder.AppendLine(sumsFileLines[i]);
		}
		bool isSignatureValid = VerifyShaSumsFile(contentBuilder.ToString().Replace("\r\n", "\n"), base64Signature, pubKey);
		if (!isSignatureValid)
		{
			throw new ArgumentException($"Couldn't verify Wasabi's signature in {ShaSumsFileName}.");
		}
		return (content, installerDictionary, base64Signature);
	}

	public static uint256 GenerateHashFromFile(string filePath)
	{
		if (File.Exists(filePath))
		{
			byte[] bytes = File.ReadAllBytes(filePath);
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

	public static void SignAndSaveSHASumsFile(string signatureFilePath, string destinationPath, Key key)
	{
		string content = File.ReadAllText(signatureFilePath);
		uint256 contentHash = GenerateHashFromString(content.Replace("\r\n", "\n"));

		ECDSASignature signature = key.Sign(contentHash);
		string base64Signature = Convert.ToBase64String(signature.ToDER());

		StringBuilder stringBuilder = new(content);
		stringBuilder.AppendLine($"-----BEGIN {WasabiSignatureHeadline}-----");
		stringBuilder.AppendLine(base64Signature);
		stringBuilder.AppendLine($"-----END {WasabiSignatureHeadline}-----");

		File.WriteAllText(destinationPath, stringBuilder.ToString());
	}

	public static void SavePrivateKeyToFile(string destinationPath, Key key)
	{
		try
		{
			if (File.Exists(destinationPath))
			{
				throw new ArgumentException("Private key file already exists.");
			}
			using StreamWriter streamWriter = new(destinationPath);
			streamWriter.WriteLine($"-----BEGIN {WasabiKeyHeadline}-----");
			streamWriter.WriteLine(key.ToString(Network.Main));
			streamWriter.WriteLine($"-----END {WasabiKeyHeadline}-----");
		}
		catch (Exception exc)
		{
			Logger.LogError(exc);
			throw;
		}
	}
}
