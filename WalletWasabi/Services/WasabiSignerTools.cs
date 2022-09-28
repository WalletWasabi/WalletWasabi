using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Services;

public static class WasabiSignerTools
{
	public static string SHASumsFileName { get; } = "SHA256SUMS.asc";

	private static Key GenerateKey() => new();

	public static PubKey GetPublicKey(Key key) => key.PubKey;

	public static bool VerifySHASumsFile(string filepath, PubKey publicKey)
	{
		(string content, string base64Signature) = ReadSHASumsContent(filepath);

		uint256 hash = GenerateHashFromString(content);

		byte[] sigBytes = Convert.FromBase64String(base64Signature);
		var wasabiSignature = ECDSASignature.FromDER(sigBytes);
		return publicKey.Verify(hash, wasabiSignature);
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

	public static uint256 GenerateHashFromString(string content)
	{
		byte[] bytes = File.ReadAllBytes(content);
		using SHA256 sha = SHA256.Create();
		byte[] computedHash = sha.ComputeHash(bytes);
		return new(computedHash);
	}

	public static void WriteAndSaveSHASumsFile(string[] filepaths, string destinationPath, Key key)
	{
		StringBuilder fileContent = new();
		foreach (string filepath in filepaths)
		{
			uint256 fileHash = GenerateHashFromFile(filepath);
			fileContent.AppendLine($"{fileHash} {filepath}");
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
		if (File.Exists(path))
		{
			throw new InvalidOperationException("Private key file already exists.");
		}
		using StreamWriter streamWriter = new(path);
		streamWriter.WriteLine("-----BEGIN WASABI PRIVATE KEY-----");
		streamWriter.WriteLine(key.ToString(Network.Main));
		streamWriter.WriteLine("-----END WASABI PRIVATE KEY-----");
	}
}
