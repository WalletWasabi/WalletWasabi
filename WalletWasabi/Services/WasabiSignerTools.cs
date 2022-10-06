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
	private static string WasabiKeyHeadline { get; } = "WASABI PRIVATE KEY";
	private static string WasabiContentHeadline { get; } = "SIGNED CONTENT";
	private static string WasabiSignatureHeadline { get; } = "WASABI SIGNATURE";
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

	public static bool VerifyShaSumsFile(string shaSumsFilePath, PubKey publicKey)
	{
		try
		{
			(string content, Dictionary<string, uint256> _, string base64Signature) = ReadShaSumsContent(shaSumsFilePath);

			uint256 hash = GenerateHashFromString(content);

			byte[] sigBytes = Convert.FromBase64String(base64Signature);
			var wasabiSignature = ECDSASignature.FromDER(sigBytes);
			return publicKey.Verify(hash, wasabiSignature);
		}
		catch (FormatException)
		{
			Logger.LogWarning("SHASums file signature was invalid, DER bytes are not in right format.");
		}
		catch (Exception exc)
		{
			Logger.LogError(exc);
		}
		return false;
	}

	public static (string content, Dictionary<string, uint256> installerDictionary, string base64Signature) ReadShaSumsContent(string shaSumsFilePath)
	{
		Dictionary<string, uint256> installerDictionary = new();
		string base64Signature = "";
		StringBuilder stringBuilder = new();

		string[] sumsFileLines = File.ReadAllLines(shaSumsFilePath);

		string? headline = sumsFileLines.FirstOrDefault();
		int contentEndIndex = Array.IndexOf(sumsFileLines, $"-----END {WasabiContentHeadline}-----");
		if (headline is null ||
			headline != $"-----BEGIN {WasabiContentHeadline}-----" ||
			contentEndIndex == -1)
		{
			throw new ArgumentException($"{ShaSumsFileName}'s content was invalid.");
		}

		for (int i = 1; i < contentEndIndex; i++)
		{
			string line = sumsFileLines[i].Trim();
			string[] splitLine = line.Split(" ");

			bool isHashValid = uint256.TryParse(splitLine[0], out uint256 installerHash);
			if (isHashValid)
			{
				string installerName = splitLine[1];
				installerDictionary.Add(installerName, installerHash);
				stringBuilder.AppendLine(line);
			}
		}
		string content = stringBuilder.ToString();

		int signatureBeginIndex = Array.IndexOf(sumsFileLines, $"-----BEGIN {WasabiSignatureHeadline}-----");
		if (signatureBeginIndex != -1)
		{
			base64Signature = sumsFileLines[signatureBeginIndex + 1].Trim();
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

	public static void SignAndSaveSHASumsFile(IEnumerable<string> installerFilepaths, string destinationPath, Key key)
	{
		StringBuilder fileContent = new();
		foreach (string filepath in installerFilepaths)
		{
			uint256 fileHash = GenerateHashFromFile(filepath);
			fileContent.AppendLine($"{fileHash} {filepath.Split("\\").Last()}");
		}
		uint256 contentHash = GenerateHashFromString(fileContent.ToString());

		ECDSASignature signature = key.Sign(contentHash);
		string base64Signature = Convert.ToBase64String(signature.ToDER());

		fileContent.Insert(0, $"-----BEGIN {WasabiContentHeadline}-----\n");
		fileContent.AppendLine($"-----END {WasabiContentHeadline}-----");
		fileContent.AppendLine($"-----BEGIN {WasabiSignatureHeadline}-----");
		fileContent.AppendLine(base64Signature);
		fileContent.AppendLine($"-----END {WasabiSignatureHeadline}-----");
		File.WriteAllText(destinationPath, fileContent.ToString());
	}

	public static void SavePrivateKeyToFile(string destionationPath, Key key)
	{
		try
		{
			if (File.Exists(destionationPath))
			{
				throw new ArgumentException("Private key file already exists.");
			}
			using StreamWriter streamWriter = new(destionationPath);
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
