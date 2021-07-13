using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests.Tor
{
	public static class CancellationTokenExtensions
	{
		/// <seealso href="https://github.com/dotnet/runtime/issues/14991#issuecomment-131221355"/>
		public static Task WhenCanceled(this CancellationToken cancellationToken)
		{
			TaskCompletionSource<bool> tcs = new();
			cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).SetResult(true), tcs);
			return tcs.Task;
		}
	}
}
