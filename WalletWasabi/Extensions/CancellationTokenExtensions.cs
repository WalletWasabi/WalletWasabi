using System.Threading;

namespace WalletWasabi.Extensions;

public static class CancellationTokenExtensions
{
	public static CancellationTokenSource CreateLinkedTokenSourceWithTimeout(this CancellationToken cancellationToken, TimeSpan timeOut)
	{
		var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		linkedCts.CancelAfter(timeOut);
		return linkedCts;
	}
}
