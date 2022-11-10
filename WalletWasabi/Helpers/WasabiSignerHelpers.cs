using NBitcoin.Crypto;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Helpers;

public class WasabiSignerHelpers
{
	private const string WasabiKeyHeadline = "WASABI PRIVATE KEY";
	private const string WasabiPrivateKeyFilePath = @"C:\wasabi\Wasabi.privkey";

	public static async Task SignSha256SumsFileAsync(string sha256sumsFilePath, Key wasabiPrivateKey)
	{
		var bytes = await File.ReadAllBytesAsync(sha256sumsFilePath).ConfigureAwait(false);

		using SHA256 sha = SHA256.Create();
		byte[] computedHash = sha.ComputeHash(bytes);
		ECDSASignature signature = wasabiPrivateKey.Sign(new uint256(computedHash));

		string base64Signature = Convert.ToBase64String(signature.ToDER());
		var wasabiSignatureFilePath = Path.ChangeExtension(sha256sumsFilePath, "wasabisig");

		await File.WriteAllTextAsync(wasabiSignatureFilePath, base64Signature).ConfigureAwait(false);
	}

	public static async Task SavePrivateKeyToFileAsync()
	{
		using Key key = new();
		var destinationFilePath = WasabiPrivateKeyFilePath;
		if (File.Exists(destinationFilePath))
		{
			throw new ArgumentException("Private key file already exists.");
		}

		IoHelpers.EnsureContainingDirectoryExists(destinationFilePath);
		using StreamWriter streamWriter = new(destinationFilePath);
		await streamWriter.WriteLineAsync($"-----BEGIN {WasabiKeyHeadline}-----").ConfigureAwait(false);
		await streamWriter.WriteLineAsync(key.ToString(Network.Main)).ConfigureAwait(false);
		await streamWriter.WriteLineAsync($"-----END {WasabiKeyHeadline}-----").ConfigureAwait(false);
	}

	public static Key GetPrivateKeyFromFile(string fileName)
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

		return secret.PrivateKey;
	}
}
