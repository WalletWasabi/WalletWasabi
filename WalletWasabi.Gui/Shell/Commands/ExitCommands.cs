using System.Composition;
using System.Threading.Tasks;
using AvalonStudio.Commands;
using ReactiveUI;
using System;
using Avalonia;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ExitCommands : IDisposable
	{
		private CompositeDisposable Disposables { get; }

		[ExportCommandDefinition("File.Exit")]
		public CommandDefinition ExitCommand { get; }

		[ImportingConstructor]
		public ExitCommands(CommandIconService commandIconService)
		{
			Disposables = new CompositeDisposable();

			var exit = ReactiveCommand.Create(OnExit).DisposeWith(Disposables);

			exit.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning<ExitCommands>(ex));

			ExitCommand = new CommandDefinition(
			   "Exit",
			   commandIconService.GetCompletionKindImage("Exit"),
			   exit);
		}

		private void OnExit()
		{
			Application.Current.MainWindow.Close();
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
