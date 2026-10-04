#if DEBUG || WASABI_RUNTIME_PROBE
using NBitcoin;
using System.Security.Cryptography;
using WabiSabi;
using WabiSabi.Crypto;

namespace WalletWasabi.Android;

internal static class RuntimeProbe
{
	public static void Verify()
	{
		Check("android-bundle", () => { using var bundle = new global::Android.OS.Bundle(); bundle.PutString("probe", "public-vector"); if (bundle.GetString("probe") != "public-vector") { throw new InvalidOperationException("Android marshaling failed."); } });
		Check("runtime-lock", () => { var gate = new System.Threading.Lock(); lock (gate) { lock (gate) { } } });
		Check("genesis", () =>
		{
			var block = Network.Main.GetGenesis();
			if (block.GetHash().ToString() != "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f") { throw new InvalidOperationException("Genesis hash mismatch."); }
			var bytes = block.ToBytes();
			if (!bytes.SequenceEqual(Block.Load(bytes, Network.Main).ToBytes())) { throw new InvalidOperationException("Serialization mismatch."); }
		});
		Check("bip39", () =>
		{
			var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
			var seed = mnemonic.DeriveSeed("TREZOR");
			if (Convert.ToHexString(seed).ToLowerInvariant() != "c55257c360c07c72029aebc1b53c05ed0362ada38ead3e3e9efa3708e53495531f09a6987599d18264c1e1c92f2cf141630c7a3c4ab7c81b2f001698e7463b04") { throw new InvalidOperationException("BIP39 vector mismatch."); }
			CryptographicOperations.ZeroMemory(seed);
		});
		Check("bip32", () =>
		{
			var root = ExtKey.CreateFromSeed(Convert.FromHexString("000102030405060708090a0b0c0d0e0f"));
			var child = root.Derive(new KeyPath("m/0'/1/2'/2/1000000000"));
			var publicChild = root.Derive(new KeyPath("m/0'/1/2'")).Neuter().Derive(new KeyPath("2/1000000000"));
			if (child.Neuter() != publicChild) { throw new InvalidOperationException("BIP32 public derivation mismatch."); }
			if (child.Neuter().GetWif(Network.Main).ToString() != "xpub6H1LXWLaKsWFhvm6RVpEL9P4KfRZSW7abD2ttkWP3SSQvnyA8FSVqNTEcYFgJS2UaFcxupHiYkro49S8yGasTvXEYBVPamhGW6cFJodrTHy") { throw new InvalidOperationException("BIP32 vector mismatch."); }
		});
		Check("bip84", () =>
		{
			var root = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about").DeriveExtKey();
			var child = root.Derive(new KeyPath("m/84'/0'/0'/0/0"));
			if (child.PrivateKey.PubKey.WitHash.GetAddress(Network.Main).ToString() != "bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu") { throw new InvalidOperationException("BIP84 vector mismatch."); }
		});
		Check("bip86", () =>
		{
			var root = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about").DeriveExtKey();
			var child = root.Derive(new KeyPath("m/86'/0'/0'/0/0"));
			if (child.PrivateKey.PubKey.GetTaprootFullPubKey().ScriptPubKey.ToHex() != "5120a60869f0dbcf1dc659c9cecbaf8050135ea9e8cdc487053f1dc6880949dc684c") { throw new InvalidOperationException("BIP86 vector mismatch."); }
		});
		Check("ecdsa", () =>
		{
			using var key = new Key(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000003"));
			var digest = new uint256(SHA256.HashData("Wasabi Android runtime vector"u8));
			if (!key.PubKey.Verify(digest, key.Sign(digest))) { throw new InvalidOperationException("ECDSA verification failed."); }
			var taproot = key.PubKey.GetTaprootFullPubKey();
			if (!taproot.ScriptPubKey.ToBytes().Any()) { throw new InvalidOperationException("Taproot construction failed."); }
		});
		// Published BIP340 vector 0, exercised through the managed secp256k1
		// implementation used by WabiSabi. No random self-consistency oracle.
		Check("bip340", () =>
		{
			using var key = NBitcoin.Secp256k1.ECPrivKey.Create(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000003"));
			var message = new byte[32];
			var signature = key.SignBIP340(message, new byte[32]);
			if (Convert.ToHexString(signature.ToBytes()) != "E907831F80848D1069A5371B402410364BDF1C5F8307B0084C55F1CE2DCA821525F66A4A85EA8B71E482A74F382D2CE5EBEEE8FDB2172F477DF4900D310536C0") { throw new InvalidOperationException("BIP340 signature vector mismatch."); }
			var publicKey = NBitcoin.Secp256k1.ECXOnlyPubKey.Create(Convert.FromHexString("F9308A019258C31049344F85F89D5229B531C845836F99B08601F113BCE036F9"));
			if (!publicKey.SigVerifyBIP340(signature, message)) { throw new InvalidOperationException("BIP340 verification vector failed."); }
			message[0] = 1;
			if (publicKey.SigVerifyBIP340(signature, message)) { throw new InvalidOperationException("Altered BIP340 message was accepted."); }
		});
		Check("wabisabi", () =>
		{
			var random = WalletWasabi.Crypto.Randomness.SecureRandom.Instance;
			var secret = new CredentialIssuerSecretKey(random);
			var issuer = new CredentialIssuer(secret, random, 100_000_000);
			var client = new WabiSabiClient(secret.ComputeCredentialIssuerParameters(), random, 100_000_000);
			var zero = client.CreateRequestForZeroAmount();
			var initial = client.HandleResponse(issuer.HandleRequest(zero.CredentialsRequest), zero.CredentialsResponseValidation);
			var request = client.CreateRequest([500_000L], initial, CancellationToken.None);
			var issued = client.HandleResponse(issuer.HandleRequest(request.CredentialsRequest), request.CredentialsResponseValidation);
			if (issued.Sum(c => c.Value) != 500_000) { throw new InvalidOperationException("WabiSabi proof verification failed."); }
		});
	}
	private static void Check(string phase, Action action)
	{
		global::Android.Util.Log.Info("WasabiRuntime", phase);
		try { action(); }
		catch (Exception ex) { throw new InvalidOperationException("Runtime phase failed: " + phase, ex); }
	}
}
#endif
