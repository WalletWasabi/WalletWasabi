using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
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
	public LabelsArray Labels => HdPubKey.Labels;
	public PubKey PubKey => HdPubKey.PubKey;
	public KeyPath FullKeyPath => HdPubKey.FullKeyPath;
	public string Text => BitcoinAddress.ToString();

	private bool IsUnused => Labels.Any() && !HdPubKey.IsInternal && HdPubKey.KeyState == KeyState.Clean;

	public bool IsUsed => !IsUnused;

	public void Hide()
	{
		// Code commented out due to https://github.com/zkSNACKs/WalletWasabi/issues/10177
		// KeyManager.SetKeyState(KeyState.Locked, HdPubKey);
		this.RaisePropertyChanged(nameof(IsUsed));
	}

	public void SetLabels(LabelsArray labels)
	{
		HdPubKey.SetLabel(labels, KeyManager);
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
			// This exception happens every time on TestNet because of Wasabi Keypath handling.
			// The user doesn't need to know about it.
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			if (cts.IsCancellationRequested)
			{
				throw new ApplicationException("User response didn't arrive in time.");
			}

			throw;
		}
	}
}
