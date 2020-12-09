namespace WalletWasabi.Fluent.Desktop
{
	public class Program
	{
		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		public static void Main(string[] args)
		{
			FluentProgram program = new();
			program.Run(args);
		}
	}
}
