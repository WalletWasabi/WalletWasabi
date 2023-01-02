using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.WabiSabi.Client;

public interface IWasabiBackendStatusProvider
{
	bool Connected { get; }
}
