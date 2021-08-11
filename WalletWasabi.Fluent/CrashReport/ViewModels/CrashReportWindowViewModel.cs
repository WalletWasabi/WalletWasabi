using System;
using System.IO;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.CrashReport.ViewModels
{
	public partial class CrashReportWindowViewModel : ViewModelBase
	{
		[AutoNotify] private SerializableException _serializableException;

		public CrashReportWindowViewModel(SerializableException exception)
		{
			SerializableException = exception;
			CancelCommand = ReactiveCommand.Create(CrashReporter.RestartWasabi);
			NextCommand = ReactiveCommand.Create(CrashReporter.ShutdownWasabi);
			OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink);
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