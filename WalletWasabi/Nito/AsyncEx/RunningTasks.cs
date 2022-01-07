using System.Threading.Tasks;

namespace WalletWasabi.Nito.AsyncEx;

public class RunningTasks : IDisposable
{
	public RunningTasks(AbandonedTasks taskCollection)
	{
		taskCollection.AddAndClearCompleted(Completion.Task);
	}

	private bool DisposedValue { get; set; } = false;
	private TaskCompletionSource Completion { get; } = new();

	public static IDisposable RememberWith(AbandonedTasks taskCollection) => new RunningTasks(taskCollection);

	protected virtual void Dispose(bool disposing)
	{
		if (!DisposedValue)
		{
			if (disposing)
			{
				Completion.TrySetResult();
			}

			DisposedValue = true;
		}
	}

	public void Dispose() => Dispose(true);
}
