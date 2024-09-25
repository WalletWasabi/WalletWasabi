using System.Threading.Tasks;

namespace WalletWasabi.Nito.AsyncEx;

public class RunningTasks : IDisposable
{
	public RunningTasks(AbandonedTasks taskCollection)
	{
		taskCollection.AddAndClearCompleted(_completion.Task);
	}

	private bool DisposedValue { get; set; } = false;
	private readonly TaskCompletionSource _completion = new();

	public static IDisposable RememberWith(AbandonedTasks taskCollection) => new RunningTasks(taskCollection);

	protected virtual void Dispose(bool disposing)
	{
		if (!DisposedValue)
		{
			if (disposing)
			{
				_completion.TrySetResult();
			}

			DisposedValue = true;
		}
	}

	public void Dispose() => Dispose(true);
}
