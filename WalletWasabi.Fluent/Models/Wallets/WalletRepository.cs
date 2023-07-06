using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletRepository : ReactiveObject, IWalletRepository
{
	private ReadOnlyObservableCollection<IWalletModel> _wallets;

	public WalletRepository()
	{
		// Convert the Wallet Manager's contents into an observable stream of IWalletModels.
		Wallets =
			Observable.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded)).Select(_ => Unit.Default)
					  .StartWith(Unit.Default)
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .SelectMany(_ => Services.WalletManager.GetWallets())
					  .ToObservableChangeSet(x => x.WalletName)
					  .TransformWithInlineUpdate(wallet => new WalletModel(wallet), (model, wallet) => { })

					  // Refresh the collection when logged in.
					  .AutoRefresh(x => x.IsLoggedIn)
					  .Transform(x => x as IWalletModel);

		// Materialize the Wallet list to determine the default wallet.
		Wallets
			.Bind(out _wallets)
			.Subscribe();

		DefaultWallet =
			_wallets.FirstOrDefault(item => item.Name == Services.UiConfig.LastSelectedWallet)
			?? _wallets.FirstOrDefault();
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IWalletModel? DefaultWallet { get; }
	public bool HasWallet => Services.WalletManager.HasWallet();

	private KeyPath AccountKeyPath { get; } = KeyManager.GetAccountKeyPath(Services.WalletManager.Network, ScriptPubKeyType.Segwit);

	public void StoreLastSelectedWallet(IWalletModel wallet)
	{
		Services.UiConfig.LastSelectedWallet = wallet.Name;
	}

	public string GetNextWalletName()
	{
		return Services.WalletManager.WalletDirectories.GetNextWalletName("Wallet");
	}
	public async Task<IWalletSettingsModel> NewWalletAsync(WalletCreationOptions options, CancellationToken? cancelToken = null)
	{
		return options switch
		{
			WalletCreationOptions.AddNewWallet add => await CreateNewWalletAsync(add),
			WalletCreationOptions.ConnectToHardwareWallet hw => await ConnectToHardwareWalletAsync(hw, cancelToken),
			WalletCreationOptions.ImportWallet import => await ImportWalletAsync(import),
			WalletCreationOptions.RecoverWallet recover => await RecoverWalletAsync(recover),
			_ => throw new InvalidOperationException($"{nameof(WalletCreationOptions)} not supported: {options?.GetType().Name}")
		};
	}
	public IWalletModel SaveWallet(IWalletSettingsModel walletSettings)
	{
		walletSettings.Save();

		return _wallets.First(x => x.Name == walletSettings.WalletName);
	}

	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName)
	{
		return WalletHelpers.ValidateWalletName(walletName);
	}

	private async Task<IWalletSettingsModel> CreateNewWalletAsync(WalletCreationOptions.AddNewWallet options)
	{
		var (walletName, password, mnemonic) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(password);

		var (keyManager, _) = await Task.Run(
				() =>
				{
					var walletGenerator = new WalletGenerator(
						Services.WalletManager.WalletDirectories.WalletsDir,
						Services.WalletManager.Network)
					{
						TipHeight = Services.BitcoinStore.SmartHeaderChain.TipHeight
					};
					return walletGenerator.GenerateWallet(walletName, password, mnemonic);
				});

		return new WalletSettingsModel(keyManager, true);
	}

	private async Task<IWalletSettingsModel> ConnectToHardwareWalletAsync(WalletCreationOptions.ConnectToHardwareWallet options, CancellationToken? cancelToken)
	{
		var (walletName, device) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(cancelToken);

		var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;
		var keyManager = await HardwareWalletOperationHelpers.GenerateWalletAsync(device, walletFilePath, Services.WalletManager.Network, cancelToken.Value);
		keyManager.SetIcon(device.WalletType);

		var result = new WalletSettingsModel(keyManager, true);
		return result;
	}

	private async Task<IWalletSettingsModel> ImportWalletAsync(WalletCreationOptions.ImportWallet options)
	{
		var (walletName, filePath) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentException.ThrowIfNullOrEmpty(filePath);

		var keyManager = await ImportWalletHelper.ImportWalletAsync(Services.WalletManager, walletName, filePath);
		return new WalletSettingsModel(keyManager, true);
	}

	private async Task<IWalletSettingsModel> RecoverWalletAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, password, mnemonic, minGapLimit) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(password);
		ArgumentNullException.ThrowIfNull(mnemonic);
		ArgumentNullException.ThrowIfNull(minGapLimit);

		var keyManager = await Task.Run(() =>
		{
			var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;

			var result = KeyManager.Recover(
				mnemonic,
				password,
				Services.WalletManager.Network,
				AccountKeyPath,
				null,
				"", // Make sure it is not saved into a file yet.
				minGapLimit.Value);

			result.AutoCoinJoin = true;

			// Set the filepath but we will only write the file later when the Ui workflow is done.
			result.SetFilePath(walletFilePath);

			return result;
		});

		return new WalletSettingsModel(keyManager, true);
	}

	public IWalletModel? GetExistingWallet(HwiEnumerateEntry device)
	{
		var existingWallet = Services.WalletManager.GetWallets(false).FirstOrDefault(x => x.KeyManager.MasterFingerprint == device.Fingerprint);
		if (existingWallet is { })
		{
			return _wallets.First(x => x.Name == existingWallet.WalletName);
		}
		return null;
	}
}
