#if DEBUG || WASABI_RELEASE_HARNESS
using Android.Runtime;
using Android.Security.Keystore;
using Javax.Crypto;
using Javax.Crypto.Spec;
using CipherMode = Javax.Crypto.CipherMode;

namespace WalletWasabi.Android;

internal sealed partial class CredentialVault
{
    // Emulator-only instrumentation exercises the production sealing/opening
    // functions without adding a non-authenticated secret API to the personal APK.
    internal void StoreFixtureSecret(string secret, string reference = "fixture")
    {
        var alias = (reference == "rpc" ? "wasabi-rpc-" : "wasabi-fixture-") + Guid.NewGuid().ToString("N");
        using var builder = new KeyGenParameterSpec.Builder(alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);
        builder.SetKeySize(256)!.SetBlockModes(KeyProperties.BlockModeGcm!)!.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone!)!.SetRandomizedEncryptionRequired(true);
        using var spec = builder.Build()!;
        using var generator = KeyGenerator.GetInstance("AES", "AndroidKeyStore")!;
        generator.Init(spec);
        using var key = generator.GenerateKey()!;
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(CipherMode.EncryptMode, key);
        cipher.UpdateAAD(AssociatedData(reference));
        Save(reference, new(alias, Convert.ToBase64String(cipher.GetIV()!), Convert.ToBase64String(SealSecret(cipher, secret))));
    }

    internal string RetrieveFixtureSecret()
    {
        if (Load("fixture") is not { } envelope) { return ""; }
        try
        {
            using var store = OpenStore();
            using var key = store.GetKey(envelope.Alias, null)?.JavaCast<ISecretKey>() ?? throw new InvalidOperationException("Fixture key lost.");
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            using var parameters = new GCMParameterSpec(128, Convert.FromBase64String(envelope.Iv));
            cipher.Init(CipherMode.DecryptMode, key, parameters);
            cipher.UpdateAAD(AssociatedData("fixture"));
            return OpenSecret(cipher, envelope);
        }
        catch (Java.Security.GeneralSecurityException) { throw new InvalidOperationException("Invalid fixture ciphertext."); }
    }

    internal void RemoveFixtureSecret() => Remove("fixture");
}
#endif
