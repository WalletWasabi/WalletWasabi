using NBitcoin;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.Models;

public abstract record WalletBackup(string? Password = null);

public record RecoveryWordsBackup(
	string? Password = null,
	Mnemonic? Mnemonic = null) : WalletBackup(Password);

public record MultiShareBackupSettings(
	byte Threshold = 2,
	byte Shares = 5);

public record MultiShareBackup(
	MultiShareBackupSettings Settings,
	string? Password = null,
	Share[]? Shares = null,
	byte CurrentShare = 0) : WalletBackup(Password);
