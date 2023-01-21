using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.Hwi;
using WalletWasabi.Logging;

namespace WalletWasabi.Bridge;

public class HardwareInterfaceClient : IHwiClient
{
	public IObservable<Result> Show(IAddress address)
	{
		var observable = Observable
			.StartAsync(ct => ShowCore(address, ct))
			.Timeout(TimeSpan.FromSeconds(5))
			.Catch<Result, TimeoutException>(_ => Observable.Return(Result.Failure("The operation has timed out. Please try again.")));

		return observable;
	}

	private async Task<Result> ShowCore(IAddress address, CancellationToken cancellationToken)
	{
		try
		{
			var client = new HwiClient(address.Network);
			await client.DisplayAddressAsync(address.HdFingerprint, address.HdPubKey.FullKeyPath, cancellationToken);
		}
		catch (FormatException ex) when (ex.Message.Contains("network") && address.Network == Network.TestNet)
		{
			// This exception happens every time on TestNet because of Wasabi Keypath handling.
			// The user doesn't need to know about it.
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return Result.Failure(ex.ToUserFriendlyString());
		}

		return Result.Success();
	}
}
