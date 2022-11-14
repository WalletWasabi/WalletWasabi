using NBitcoin.Crypto;
using NBitcoin;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace WalletWasabi.Helpers;

public class WasabiSignerHelpers
{
	public static async Task SignSha256SumsFileAsync(string sha256SumsAscFilePath, Key wasabiPrivateKey)
	{
		var bytes = await File.ReadAllBytesAsync(sha256SumsAscFilePath).ConfigureAwait(false);
		using SHA256 sha = SHA256.Create();
		byte[] computedHash = sha.ComputeHash(bytes);

		ECDSASignature signature = wasabiPrivateKey.Sign(new uint256(computedHash));

		string base64Signature = Convert.ToBase64String(signature.ToDER());
		var wasabiSignatureFilePath = Path.ChangeExtension(sha256SumsAscFilePath, "wasabisig");

		await File.WriteAllTextAsync(wasabiSignatureFilePath, base64Signature).ConfigureAwait(false);
	}

	public static async Task VerifySha256SumsFileAsync(string sha256SumsAscFilePath)
	{
		// Read the content file
		var bytes = await File.ReadAllBytesAsync(sha256SumsAscFilePath).ConfigureAwait(false);
		using SHA256 sha = SHA256.Create();
		byte[] hash = sha.ComputeHash(bytes);

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
}
