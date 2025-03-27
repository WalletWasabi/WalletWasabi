using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletBackup(string? Password = null);

public record RecoveryWordsBackup(
	string? Password = null,
	Mnemonic? Mnemonic = null) : WalletBackup(Password);

public record MultiShareBackupSettings(
	byte Threshold = KeyManager.DefaultShamirThreshold,
	byte Shares = KeyManager.DefaultShamirShares);

public record MultiShareBackup(
	MultiShareBackupSettings Settings,
	string? Password = null,
	Share[]? Shares = null,
	byte CurrentShare = 0) : WalletBackup(Password);
