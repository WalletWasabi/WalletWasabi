using System;
using System.IO;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport.ViewModels
{
	public class CrashReportWindowViewModel : ViewModelBase
	{
		public CrashReportWindowViewModel(SerializableException serializedException)
		{
			SerializedSerializedException = serializedException;
			CancelCommand = ReactiveCommand.Create(CrashReporter.RestartWasabi);
			NextCommand = ReactiveCommand.Create(CrashReporter.ShutdownWasabi);
			OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink);
			});
		}

		public SerializableException SerializedSerializedException { get; }

		public ICommand OpenGitHubRepoCommand { get; }

		public ICommand NextCommand { get; }

		public ICommand CancelCommand { get; }

		public string Caption => $"A problem has occurred and Wasabi is unable to continue.";

		public string Trace => $"{SerializedSerializedException.Message}{Environment.NewLine}" +
		                       $"{Environment.NewLine}{SerializedSerializedException.StackTrace}";

		public string Title => "Wasabi has crashed.";
	}
}