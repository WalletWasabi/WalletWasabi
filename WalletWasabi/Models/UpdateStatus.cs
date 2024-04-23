namespace WalletWasabi.Models;

public record UpdateStatus(
	bool BackendCompatible,
	bool ClientUpToDate,
	Version LegalDocumentsVersion,
	ushort CurrentBackendMajorVersion,
	Version ClientVersion,
	bool IsReadyToInstall = false);
