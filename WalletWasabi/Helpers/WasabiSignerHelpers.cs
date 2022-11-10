using NBitcoin.Crypto;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace WalletWasabi.Helpers;

public class WasabiSignerHelpers
{
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
}
