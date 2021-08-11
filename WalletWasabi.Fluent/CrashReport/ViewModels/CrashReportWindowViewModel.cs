using System;
using System.IO;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.CrashReport.ViewModels
{
	public partial class CrashReportWindowViewModel : ViewModelBase
	{
		[AutoNotify] private SerializableException _serializableException;
		[AutoNotify] private string _logPath;

		public CrashReportWindowViewModel(SerializableException exception, string logPath)
		{
			SerializableException = exception;
			LogPath = logPath;
			CancelCommand = ReactiveCommand.Create(CrashReporter.RestartWasabi);
			NextCommand = ReactiveCommand.Create(CrashReporter.ShutdownWasabi);
			OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await IoHelpers.OpenBrowserAsync("https://github.com/zkSNACKs/WalletWasabi/issues");
			});
		}

		public ICommand OpenGitHubRepoCommand { get; }
		public ICommand NextCommand { get; }
		public ICommand CancelCommand { get; }

		public string Caption => $"A problem has occurred and Wasabi is unable to continue.";

		public string Trace => $"{_serializableException.Message}{Environment.NewLine}" +
		                       $"{Environment.NewLine}{_serializableException.StackTrace}";

		public string Title => "Wasabi has crashed.";
	}
}