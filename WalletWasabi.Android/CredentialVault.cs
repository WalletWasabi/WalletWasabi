using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Android.Runtime;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;
using WalletWasabi.Io;
using WalletWasabi.Mobile;
using CipherMode = Javax.Crypto.CipherMode;

namespace WalletWasabi.Android;

internal sealed partial class CredentialVault(Context context, string dataDirectory) : ICredentialVault
{
	private sealed record Envelope(string Alias, string Iv, string Ciphertext);
	private string DirectoryPath => Path.Combine(dataDirectory, "vault");
	private string FilePath(string reference) => Path.Combine(DirectoryPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference))) + ".json");
	private static KeyStore OpenStore() { var store = KeyStore.GetInstance("AndroidKeyStore")!; store.Load(null); return store; }
	private bool Exists(string reference) => File.Exists(FilePath(reference)) || File.Exists(FilePath(reference) + ".old");
	public bool HasWalletPassword(string walletReference) => Exists("wallet:" + walletReference);
	public bool CanEnrollWalletPassword => OperatingSystem.IsAndroidVersionAtLeast(30)
		&& context is Activity && context.GetSystemService(Context.BiometricService) is BiometricManager manager
		&& (int)manager.CanAuthenticate(0x000f | 0x8000) == (int)BiometricCode.Success;

	public async Task EnrollWalletPasswordAsync(string walletReference, string originalPassword, CancellationToken cancellationToken)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(30) || context is not Activity activity) { throw new InvalidOperationException("Device unlocking requires Android 11 or newer. Use your original wallet password."); }
		var reference = "wallet:" + walletReference;
		var alias = "wasabi-wallet-" + Guid.NewGuid().ToString("N");
		var committed = false;
		using var store = OpenStore();
		try
		{
			using var key = CreateKey(alias);
			RequireHardware(key);
			using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
			cipher.Init(CipherMode.EncryptMode, key);
			await AuthenticateAsync(activity, cipher, "Protect wallet access", cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			// Even AAD is a KeyMint operation. Sending it before the per-use
			// grant can cache an authentication failure inside the cipher.
			cipher.UpdateAAD(AssociatedData(reference));
			var encrypted = SealSecret(cipher, originalPassword);
			Envelope? old;
			try { old = Load(reference); }
			catch (Exception error) when (error is IOException or JsonException or FormatException) { old = null; }
			Save(reference, new(alias, Convert.ToBase64String(cipher.GetIV()!), Convert.ToBase64String(encrypted)));
			committed = true;
			if (old is not null) { store.DeleteEntry(old.Alias); }
		}
		catch (Exception error) when (error is Java.Security.GeneralSecurityException or Java.Security.ProviderException)
		{
			if (!committed) { store.DeleteEntry(alias); }
			throw new PlatformNotSupportedException("Device unlocking is unavailable. Use your original wallet password.", error);
		}
		catch { if (!committed) { store.DeleteEntry(alias); } throw; }
	}

	public async Task<string> RetrieveWalletPasswordAsync(string walletReference, string purpose, CancellationToken cancellationToken)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(30) || context is not Activity activity) { throw new InvalidOperationException("Use your original wallet password on this Android version."); }
		var reference = "wallet:" + walletReference;
		var envelope = Load(reference) ?? throw new InvalidOperationException("Enter the original wallet password to restore device unlocking.");
		try
		{
			using var store = OpenStore();
			using var key = store.GetKey(envelope.Alias, null)?.JavaCast<ISecretKey>() ?? throw new InvalidOperationException("The device key is unavailable. Enter the original wallet password.");
			RequireHardware(key);
			using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
			using var parameters = new GCMParameterSpec(128, Convert.FromBase64String(envelope.Iv));
			cipher.Init(CipherMode.DecryptMode, key, parameters);
			await AuthenticateAsync(activity, cipher, purpose, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			cipher.UpdateAAD(AssociatedData(reference));
			return OpenSecret(cipher, envelope);
		}
		catch (Exception error) when (error is Java.Security.GeneralSecurityException or Java.Security.ProviderException)
		{
			throw new InvalidOperationException("Device unlocking is unavailable. Enter the original wallet password; your wallet and backup are unchanged.");
		}
	}

	public void RemoveWalletPassword(string walletReference) => Remove("wallet:" + walletReference);
	public void RemoveRetiredNodeCredentials()
	{
		const string reference = "rpc";
		if (!Exists(reference)) { return; }
		using var store = OpenStore();
		var retired = new List<string>();
		using var aliases = store.Aliases()!;
		while (aliases.HasMoreElements)
		{
			using var item = aliases.NextElement();
			if (item?.ToString() is { } alias && alias.StartsWith("wasabi-rpc-", StringComparison.Ordinal)) { retired.Add(alias); }
		}
		foreach (var alias in retired) { store.DeleteEntry(alias); }
		File.Delete(FilePath(reference));
		File.Delete(FilePath(reference) + ".old");
	}

	private static byte[] SealSecret(Cipher cipher, string secret)
	{
		var bytes = Encoding.UTF8.GetBytes(secret);
		try { return cipher.DoFinal(bytes)!; }
		finally { CryptographicOperations.ZeroMemory(bytes); }
	}

	private static string OpenSecret(Cipher cipher, Envelope envelope)
	{
		var bytes = cipher.DoFinal(Convert.FromBase64String(envelope.Ciphertext))!;
		try { return Encoding.UTF8.GetString(bytes); }
		finally { CryptographicOperations.ZeroMemory(bytes); }
	}

	private static ISecretKey CreateKey(string alias)
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(28))
		{
			try { return Generate(alias, strongBox: true); }
			catch (Java.Security.GeneralSecurityException) { }
			catch (Java.Security.ProviderException) { }
		}
		return Generate(alias, strongBox: false);
	}

	private static ISecretKey Generate(string alias, bool strongBox)
	{
		using var builder = new KeyGenParameterSpec.Builder(alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);
		builder.SetKeySize(256)!.SetBlockModes(KeyProperties.BlockModeGcm!)!.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone!)!.SetRandomizedEncryptionRequired(true)!.SetUserAuthenticationRequired(true);
		if (OperatingSystem.IsAndroidVersionAtLeast(28)) { builder.SetIsStrongBoxBacked(strongBox); }
		if (OperatingSystem.IsAndroidVersionAtLeast(30))
		{
			builder.SetUserAuthenticationParameters(0, (int)(KeyPropertiesAuthType.BiometricStrong | KeyPropertiesAuthType.DeviceCredential));
			builder.SetInvalidatedByBiometricEnrollment(false);
		}
		using var spec = builder.Build()!;
		using var generator = KeyGenerator.GetInstance("AES", "AndroidKeyStore")!;
		generator.Init(spec);
		return generator.GenerateKey()!;
	}

	private static void RequireHardware(ISecretKey key)
	{
		using var factory = SecretKeyFactory.GetInstance("AES", "AndroidKeyStore")!;
		using var info = factory.GetKeySpec(key, Java.Lang.Class.FromType(typeof(KeyInfo)))!.JavaCast<KeyInfo>();
		var hardware = OperatingSystem.IsAndroidVersionAtLeast(31) ? (int)info.SecurityLevel is 1 or 2 : info.IsInsideSecureHardware;
		if (!hardware) { throw new PlatformNotSupportedException("This device has no hardware-backed authenticated Keystore. Use password unlocking."); }
	}

	private byte[] AssociatedData(string reference) => Encoding.UTF8.GetBytes(context.PackageName + ":" + reference);
	private Envelope? Load(string reference) => Exists(reference) ? JsonSerializer.Deserialize<Envelope>(File.SafelyReadAllText(FilePath(reference), Encoding.UTF8)) ?? throw new IOException("The credential vault is invalid. Use the original wallet password.") : null;
	private void Save(string reference, Envelope envelope) { Directory.CreateDirectory(DirectoryPath); File.SafelyWriteAllText(FilePath(reference), JsonSerializer.Serialize(envelope), Encoding.UTF8); }
	private void Remove(string reference)
	{
		var envelope = Load(reference);
		if (envelope is not null) { using var store = OpenStore(); store.DeleteEntry(envelope.Alias); File.Delete(FilePath(reference)); File.Delete(FilePath(reference) + ".old"); }
	}

	[SupportedOSPlatform("android30.0")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Android owns pending prompt callbacks beyond cancellation; Java peers must survive until the framework releases them.")]
	private static async Task AuthenticateAsync(Activity activity, Cipher cipher, string purpose, CancellationToken cancellationToken)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(30)) { throw new PlatformNotSupportedException(); }
		cancellationToken.ThrowIfCancellationRequested();
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		// Android can deliver a terminal callback after cancellation completes the
		// managed await. Let its Java peers retire normally rather than disposing
		// them while the framework or the queued UI action still holds them.
		var signal = new CancellationSignal();
		using var cancellation = cancellationToken.Register(() => { signal.Cancel(); completion.TrySetCanceled(cancellationToken); });
		var callback = new Authentication(completion);
		var crypto = new BiometricPrompt.CryptoObject(cipher);
		using var builder = new BiometricPrompt.Builder(activity);
		var prompt = builder.SetTitle(purpose)!.SetAllowedAuthenticators(0x000f | 0x8000)!.SetConfirmationRequired(true)!.Build()!;
		activity.RunOnUiThread(() =>
		{
			if (completion.Task.IsCompleted) { return; }
			if (activity.IsFinishing || activity.IsDestroyed)
			{ completion.TrySetException(new System.OperationCanceledException("Device authentication was cancelled.")); return; }
			try
			{
				if (OperatingSystem.IsAndroidVersionAtLeast(30)) { prompt.Authenticate(crypto, signal, activity.MainExecutor!, callback); }
			}
			catch (System.Exception error) { completion.TrySetException(error); }
		});
		await completion.Task.ConfigureAwait(false);
	}

	[SupportedOSPlatform("android30.0")]
	private sealed class Authentication(TaskCompletionSource completion) : BiometricPrompt.AuthenticationCallback
	{
		public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)
		{
			if (completion.Task.IsCompleted) { return; }
			if (result?.CryptoObject?.Cipher is not null) { completion.TrySetResult(); }
			else { completion.TrySetException(new UnauthorizedAccessException("Authentication did not authorize decryption.")); }
		}
		public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString)
		{
			global::Android.Util.Log.Warn("WasabiWallet", "Device authentication ended with code " + (int)errorCode);
			completion.TrySetException(new System.OperationCanceledException("Device authentication was cancelled or unavailable."));
		}
	}
}
