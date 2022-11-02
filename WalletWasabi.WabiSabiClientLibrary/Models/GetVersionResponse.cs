namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record GetVersionResponse(
	string Version,
	string CommitHash,
	bool Debug
);
