using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public record CoinVerifyResult(Coin Coin, bool ShouldBan, bool ShouldRemove, Reason Reason, ApiResponseItem? ApiResponseItem = null, Exception? Exception = null);

public enum Reason
{
	Remix,
	Whitelisted,
	OneHop,
	RemoteApiChecked,
	NotChecked,
	Immature,
	Exception
}
