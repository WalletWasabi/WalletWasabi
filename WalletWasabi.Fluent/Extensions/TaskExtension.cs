using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Extensions;

public static class TaskExtension
{
	public static void FireAndForget(this Task task)
	{
		_ = RunAsync(task);
	}

	private static async Task RunAsync(Task task)
	{
		try
		{
			await task;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}
}
