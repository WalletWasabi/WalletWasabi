using DynamicData;
using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletRepository : ReactiveObject
{
	private readonly IServices _services;
	private readonly AmountProvider _amountProvider;
	private readonly Dictionary<WalletId, WalletModel> _walletDictionary = new();
	private readonly CompositeDisposable _disposable = new();

	public WalletRepository(IServices services, AmountProvider amountProvider)
	{
		_services = services;
		_amountProvider = amountProvider;

		var signals =
			Observable.FromEventPattern<Wallet>(services.WalletManager, nameof(WalletManager.WalletAdded))
					  .Select(_ => System.Reactive.Unit.Default)
					  .StartWith(System.Reactive.Unit.Default);

		Wallets =
			signals.Fetch(() => services.GetWallets(), x => x.WalletId)
				   .DisposeWith(_disposable)
				   .Connect()
				   .TransformWithInlineUpdate(CreateWalletModel, (_, _) => { })
				   .Cast(x => (IWalletModel)x)
				   .AsObservableCache()
				   .DisposeWith(_disposable);

		DefaultWalletName = services.GetLastSelectedWallet();
	}

	public IObservableCache<IWalletModel, WalletId> Wallets { get; }

	public string? DefaultWalletName { get; }
	public bool HasWallet => _services.HasWallet();

	private KeyPath AccountKeyPath => KeyManager.GetAccountKeyPath(_services.GetNetwork(), ScriptPubKeyType.Segwit);

	public void StoreLastSelectedWallet(IWalletModel wallet)
	{
		_services.SetLastSelectedWallet(wallet.Name);
	}

	public string GetNextWalletName()
	{
		return _services.GetNextWalletName("Wallet");
	}

	public async Task<WalletSettingsModel> NewWalletAsync(WalletCreationOptions options, CancellationToken? cancelToken = null)
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

	public IWalletModel SaveWallet(WalletSettingsModel walletSettings)
	{
		var id = walletSettings.Save();
		var result = GetById(id);
		result.Settings.IsCoinJoinPaused = walletSettings.IsCoinJoinPaused;
		return result;
	}

	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName)
	{
		return _services.ValidateWalletName(walletName);
	}

	public IWalletModel? GetExistingWallet(HwiEnumerateEntry device)
	{
		var existingWallet = _services.GetWallets().FirstOrDefault(x => x.KeyManager.MasterFingerprint == device.Fingerprint);
		if (existingWallet is { })
		{
			return GetById(existingWallet.WalletId);
		}
		return null;
	}

	private async Task<WalletSettingsModel> CreateNewWalletAsync(WalletCreationOptions.AddNewWallet options)
	{
		var (walletName, walletBackup, _) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(walletBackup);
		ArgumentNullException.ThrowIfNull(walletBackup.Password);

		var keyManager = await Task.Run(
				() =>
				{
					var walletGenerator = new WalletGenerator(
						_services.GetWalletsDir(),
						_services.GetNetwork())
					{
						TipHeight = _services.GetTipHeight()
					};

					return walletBackup switch
					{
						RecoveryWordsBackup recoveryWordsBackup =>
							walletGenerator.GenerateWallet(
								walletName,
								recoveryWordsBackup.Password,
								recoveryWordsBackup.Mnemonic).KeyManager,
						MultiShareBackup multiShareBackup =>
							walletGenerator.GenerateWallet(
								walletName,
								multiShareBackup.Password,
								multiShareBackup.Shares.Take(multiShareBackup.Settings.Threshold).ToArray()).KeyManager,
						_ => throw new ArgumentOutOfRangeException(nameof(walletBackup))
					};
				});

		return new WalletSettingsModel(_services, keyManager, true);
	}

	private async Task<WalletSettingsModel> ConnectToHardwareWalletAsync(WalletCreationOptions.ConnectToHardwareWallet options, CancellationToken? cancelToken)
	{
		var walletName = options.WalletName;
		var device = options.Device;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(cancelToken);

		var walletFilePath = _services.GetWalletFilePath(walletName);
		var keyManager = await HardwareWalletOperationHelpers.GenerateWalletAsync(device, walletFilePath, _services.GetNetwork(), cancelToken.Value, options.EnableCoinjoin);
		keyManager.SetIcon(device.WalletType);

		var result = new WalletSettingsModel(_services, keyManager, true);
		return result;
	}

	private async Task<WalletSettingsModel> ImportWalletAsync(WalletCreationOptions.ImportWallet options)
	{
		var (walletName, filePath) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentException.ThrowIfNullOrEmpty(filePath);

		var keyManager = await ImportWalletHelper.ImportWalletAsync(_services.WalletManager, walletName, filePath);
		return new WalletSettingsModel(_services, keyManager, true);
	}

	private async Task<WalletSettingsModel> RecoverWalletAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, walletBackup, minGapLimit, birthHeight) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(minGapLimit);
		ArgumentNullException.ThrowIfNull(walletBackup);

		var keyManager = await Task.Run(() =>
		{
			var walletFilePath = _services.GetWalletFilePath(walletName);

			var result = walletBackup switch
			{
				RecoveryWordsBackup recoveryWordsBackup =>
					KeyManager.Recover(
						recoveryWordsBackup.Mnemonic,
						recoveryWordsBackup.Password,
						_services.GetNetwork(),
						AccountKeyPath,
						null,
						"", // Make sure it is not saved into a file yet.
						minGapLimit.Value,
						birthHeight: birthHeight),
				MultiShareBackup multiShareBackup =>
					KeyManager.Recover(
						multiShareBackup.Shares,
						multiShareBackup.Password,
						_services.GetNetwork(),
						AccountKeyPath,
						null,
						"", // Make sure it is not saved into a file yet.
						minGapLimit.Value,
						birthHeight: birthHeight),
				_ => throw new ArgumentOutOfRangeException(nameof(walletBackup))
			};

			// Set the filepath but we will only write the file later when the Ui workflow is done.
			result.SetFilePath(walletFilePath);

			return result;
		});

		return new WalletSettingsModel(_services, keyManager, true, true);
	}

	private IWalletModel GetById(WalletId id)
	{
		return
			_walletDictionary.TryGetValue(id, out var wallet)
			? wallet
			: throw new InvalidOperationException($"Wallet not found: {id}");
	}

	private WalletModel CreateWalletModel(Wallet wallet)
	{
		if (_walletDictionary.TryGetValue(wallet.WalletId, out var existing))
		{
			if (!ReferenceEquals(existing.Wallet, wallet))
			{
				throw new InvalidOperationException($"Different instance of: {wallet.WalletName}");
			}
			return existing;
		}

		var result =
			wallet.KeyManager.IsHardwareWallet
			? new HardwareWalletModel(_services, wallet, _amountProvider)
			: new WalletModel(_services, wallet, _amountProvider);

		_walletDictionary[wallet.WalletId] = result;

		return result;
	}
}
