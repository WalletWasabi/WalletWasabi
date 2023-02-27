using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Extensions;
using WalletWasabi.Hwi;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.Wallets;

public class Address : ReactiveObject, IAddress
{
	public Address(KeyManager keyManager, HdPubKey hdPubKey)
	{
		KeyManager = keyManager;
		HdPubKey = hdPubKey;
		Network = keyManager.GetNetwork();
		HdFingerprint = KeyManager.MasterFingerprint;
		BitcoinAddress = HdPubKey.GetAddress(Network);
	}

	public KeyManager KeyManager { get; }
	public HdPubKey HdPubKey { get; }
	public Network Network { get; }
	public HDFingerprint? HdFingerprint { get; }
	public BitcoinAddress BitcoinAddress { get; }
	public SmartLabel Label => HdPubKey.Label;
	public PubKey PubKey => HdPubKey.PubKey;
	public KeyPath FullKeyPath => HdPubKey.FullKeyPath;
	public string Text => BitcoinAddress.ToString();
	public IEnumerable<string> Labels => Label;

	private bool IsUnused => !Label.IsEmpty && !HdPubKey.IsInternal && HdPubKey.KeyState == KeyState.Clean;

	public bool IsUsed => !IsUnused;

	public void Hide()
	{
		// Code commented out due to https://github.com/zkSNACKs/WalletWasabi/issues/10177
		// KeyManager.SetKeyState(KeyState.Locked, HdPubKey);
		this.RaisePropertyChanged(nameof(IsUsed));
	}

	public void SetLabels(IEnumerable<string> labels)
	{
		HdPubKey.SetLabel(new SmartLabel(labels.ToList()), KeyManager);
		this.RaisePropertyChanged(nameof(Labels));
	}

	public async Task ShowOnHwWalletAsync()
	{
		if (HdFingerprint is null)
		{
			return;
		}

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		try
		{
			var client = new HwiClient(Network);
			await client.DisplayAddressAsync(HdFingerprint.Value, FullKeyPath, cts.Token).ConfigureAwait(false);
		}
		catch (FormatException ex) when (ex.Message.Contains("network") && Network == Network.TestNet)
		{
			// This exception happens everytime on TestNet because of Wasabi Keypath handling.
			// The user doesn't need to know about it.
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var exMessage = cts.IsCancellationRequested ? "User response didn't arrive in time." : ex.ToUserFriendlyString();
			throw;
		}
	}
}
