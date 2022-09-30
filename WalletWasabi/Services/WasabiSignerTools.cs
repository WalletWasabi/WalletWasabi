using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Services;

public static class WasabiSignerTools
{
	private static string WasabiKeyHeadline { get; } = "WASABI PRIVATE KEY";
	public static string SHASumsFileName { get; } = "SHA256SUMS.asc";

	public static Key GenerateKey() => new();

	// PublicKey should be saved in Constants.cs later on
	public static PubKey GetPublicKey() => Constants.WasabiPublicKey;

	public static bool TryGetKeyFromFile(string fileName, [NotNullWhen(true)] out Key? key)
	{
		try
		{
			using StreamReader streamReader = new(fileName);
			bool endRequested = false;
			string wif = "";
			while (!endRequested)
			{
				var line = streamReader.ReadLine();
				if (line is null)
				{
					endRequested = true;
				}
				else if (line.Contains(WasabiKeyHeadline))
				{
					wif = streamReader.ReadLine()!;
					endRequested = true;
				}
			}
			BitcoinSecret secret = new(wif, Network.Main);
			key = secret.PrivateKey;
			return true;
		}
		catch (Exception exc)
		{
			Logger.LogError("There was an error while reading Key from file.", exc);
			key = null;
			return false;
		}
	}

	public static bool VerifySHASumsFile(string filepath, PubKey publicKey)
	{
		try
		{
			(string content, string base64Signature) = ReadSHASumsContent(filepath);

			uint256 hash = GenerateHashFromString(content);

			byte[] sigBytes = Convert.FromBase64String(base64Signature);
			var wasabiSignature = ECDSASignature.FromDER(sigBytes);
			return publicKey.Verify(hash, wasabiSignature);
		}
		catch (FormatException)
		{
			Logger.LogWarning("SHASums file signature was invalid, DER bytes are not in right format.");
			return false;
		}
		catch (Exception exc)
		{
			Logger.LogError(exc);
			return false;
		}
	}

	public static uint256 ReadHashFromFile(string filepath, string installerName)
	{
		(string content, string _) = ReadSHASumsContent(filepath);
		var lines = content.Split("\n");
		foreach (var line in lines)
		{
			if (string.IsNullOrEmpty(line))
			{
				continue;
			}

			var splitLine = line.Split(" ");
			var fileName = splitLine[1].Trim();

			if (fileName == installerName)
			{
				return new(splitLine[0].Trim());
			}
		}
		throw new ArgumentException($"{installerName} not found in {filepath}");
	}

	private static (string content, string base64Signature) ReadSHASumsContent(string filepath)
	{
		bool endRequested = false;
		string base64Signature = "";
		StringBuilder stringBuilder = new();

		using StreamReader reader = new(filepath);
		while (!endRequested)
		{
			string? line = reader.ReadLine();
			if (line is null)
			{
				endRequested = true;
			}
			else if (line.Contains("WASABI"))
			{
				string? nextLine = reader.ReadLine();

				if (nextLine is not null)
				{
					base64Signature = nextLine;
				}
				endRequested = true;
			}
			else if (!line.Contains("-----"))
			{
				stringBuilder.AppendLine(line);
			}
		}
		string content = stringBuilder.ToString();
		return (content, base64Signature);
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

	public static void SignAndSaveSHASumsFile(IEnumerable<string> filepaths, string destinationPath, Key key)
	{
		StringBuilder fileContent = new();
		foreach (string filepath in filepaths)
		{
			uint256 fileHash = GenerateHashFromFile(filepath);
			fileContent.AppendLine($"{fileHash} {filepath.Split("\\").Last()}");
		}
		uint256 contentHash = GenerateHashFromString(fileContent.ToString());

		ECDSASignature signature = key.Sign(contentHash);
		string base64Signature = Convert.ToBase64String(signature.ToDER());

		fileContent.Insert(0, "-----BEGIN SIGNED CONTENT-----\n");
		fileContent.AppendLine("-----END SIGNED CONTENT-----");
		fileContent.AppendLine("-----BEGIN WASABI SIGNATURE-----");
		fileContent.AppendLine(base64Signature);
		fileContent.AppendLine("-----END WASABI SIGNATURE-----");
		File.WriteAllText(destinationPath, fileContent.ToString());
	}

	public static void SavePrivateKeyToFile(string path, Key key)
	{
		try
		{
			if (File.Exists(path))
			{
				throw new ArgumentException("Private key file already exists.");
			}
			using StreamWriter streamWriter = new(path);
			streamWriter.WriteLine($"-----BEGIN {WasabiKeyHeadline}-----");
			streamWriter.WriteLine(key.ToString(Network.Main));
			streamWriter.WriteLine($"-----END {WasabiKeyHeadline}-----");
		}
		catch (Exception)
		{
			throw;
		}
	}
}
