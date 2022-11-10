using NBitcoin.Crypto;
using NBitcoin;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace WalletWasabi.Helpers;

public class WasabiSignerHelpers
{
	private const string WasabiPrivateKeyFilePath = @"C:\wasabi\Wasabi.privkey";
	private const string WasabiPublicKeyFilePath = @"C:\wasabi\Wasabi.pubkey";

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

	public static async Task GeneratePrivateAndPublicKeyToFileAsync()
	{
		using Key key = new();
		if (File.Exists(WasabiPrivateKeyFilePath))
		{
			throw new ArgumentException("Private key file already exists.");
		}

		IoHelpers.EnsureContainingDirectoryExists(WasabiPrivateKeyFilePath);

		await File.WriteAllTextAsync(WasabiPrivateKeyFilePath, key.ToString()).ConfigureAwait(false);
		await File.WriteAllTextAsync(WasabiPublicKeyFilePath, key.PubKey.ToString()).ConfigureAwait(false);
	}

	public static async Task<Key> GetPrivateKeyFromFileAsync()
	{
		string fileName = WasabiPrivateKeyFilePath;
		string keyFileContent = await File.ReadAllTextAsync(fileName).ConfigureAwait(false);
		BitcoinSecret secret = new(keyFileContent, Network.Main);
		return secret.PrivateKey;
	}
}
