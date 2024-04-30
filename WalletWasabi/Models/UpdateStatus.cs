namespace WalletWasabi.Models;

public record UpdateStatus(bool ClientUpToDate, bool BackendCompatible, bool IsReadyToInstall, Version ClientVersion);
