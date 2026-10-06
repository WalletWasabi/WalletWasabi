namespace WalletWasabi.Mobile;

public interface ICredentialVault
{
  bool HasWalletPassword(string walletReference);
  Task<string> RetrieveWalletPasswordAsync(string walletReference, string purpose, CancellationToken cancellationToken);
  Task EnrollWalletPasswordAsync(string walletReference, string originalPassword, CancellationToken cancellationToken);
  void RemoveWalletPassword(string walletReference);
}
