using System.Threading.Tasks;

namespace WalletWasabi.Tests;

// Taken from https://stackoverflow.com/a/44718318
public static class AssertAsync
{
	public static void CompletesIn(int timeout, Action action)
	{
		var task = Task.Run(action);
		var completedInTime = Task.WaitAll(new[] { task }, TimeSpan.FromSeconds(timeout));

		if (task.Exception != null)
		{
			if (task.Exception.InnerExceptions.Count == 1)
			{
				throw task.Exception.InnerExceptions[0];
			}

			throw task.Exception;
		}

		if (!completedInTime)
		{
			throw new TimeoutException($"Task did not complete in {timeout} seconds.");
		}
	}
}
