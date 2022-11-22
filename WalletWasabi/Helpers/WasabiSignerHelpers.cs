using NBitcoin.Crypto;
using NBitcoin;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace WalletWasabi.Helpers;

public class WasabiSignerHelpers
{
	public static async Task SignSha256SumsFileAsync(string sha256SumsAscFilePath, Key wasabiPrivateKey)
	{
		var computedHash = await GetShaComputedBytesOfFileAsync(sha256SumsAscFilePath).ConfigureAwait(false);

		ECDSASignature signature = wasabiPrivateKey.Sign(new uint256(computedHash));

		string base64Signature = Convert.ToBase64String(signature.ToDER());
		var wasabiSignatureFilePath = Path.ChangeExtension(sha256SumsAscFilePath, "wasabisig");

		await File.WriteAllTextAsync(wasabiSignatureFilePath, base64Signature).ConfigureAwait(false);
	}

	public static async Task VerifySha256SumsFileAsync(string sha256SumsAscFilePath)
	{
		// Read the content file
		byte[] hash = await GetShaComputedBytesOfFileAsync(sha256SumsAscFilePath).ConfigureAwait(false);

		// Read the signature file
		var wasabiSignatureFilePath = Path.ChangeExtension(sha256SumsAscFilePath, "wasabisig");
		string signatureText = await File.ReadAllTextAsync(wasabiSignatureFilePath).ConfigureAwait(false);
		byte[] sigBytes = Convert.FromBase64String(signatureText);
		var wasabiSignature = ECDSASignature.FromDER(sigBytes);

		var pubKey = Constants.WasabiPubKey;

		if (!pubKey.Verify(new uint256(hash), wasabiSignature))
		{
			throw new InvalidOperationException("Invalid wasabi signature.");
		}
	}

	public static async Task GeneratePrivateAndPublicKeyToFileAsync(string wasabiPrivateKeyFilePath, string wasabiPublicKeyFilePath)
	{
		if (File.Exists(wasabiPrivateKeyFilePath))
		{
			throw new ArgumentException("Private key file already exists.");
		}

		IoHelpers.EnsureContainingDirectoryExists(wasabiPrivateKeyFilePath);

		using Key key = new();
		await File.WriteAllTextAsync(wasabiPrivateKeyFilePath, key.ToString(Network.Main)).ConfigureAwait(false);
		await File.WriteAllTextAsync(wasabiPublicKeyFilePath, key.PubKey.ToString()).ConfigureAwait(false);
	}

	public static async Task<Key> GetPrivateKeyFromFileAsync(string wasabiPrivateKeyFilePath)
	{
		string keyFileContent = await File.ReadAllTextAsync(wasabiPrivateKeyFilePath).ConfigureAwait(false);
		BitcoinSecret secret = new(keyFileContent, Network.Main);
		return secret.PrivateKey;
	}

	/// <summary>
	/// This function returns a SHA256 computed byte array of a file on the provided file path.
	/// </summary>
	/// <exception cref="FileNotFoundException"></exception>
	public static async Task<byte[]> GetShaComputedBytesOfFileAsync(string filePath, CancellationToken cancellationToken = default)
	{
		byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
		using SHA256 sha = SHA256.Create();
		byte[] computedHash = sha.ComputeHash(bytes);
		return computedHash;
	}
}
