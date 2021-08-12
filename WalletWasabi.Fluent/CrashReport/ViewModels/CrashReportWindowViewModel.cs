using System;
using System.IO;
using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport.ViewModels
{
	public class CrashReportWindowViewModel : ViewModelBase
	{
		public CrashReportWindowViewModel(SerializableException serializedException)
		{
			SerializedException = serializedException;
			CancelCommand = ReactiveCommand.Create(AppLifetimeHelper.Restart);
			NextCommand = ReactiveCommand.Create(AppLifetimeHelper.Shutdown);
			OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink);
			});

			if (SerializedException.ExceptionType.Contains(nameof(WasabiAlreadyRunningException),
				StringComparison.Ordinal))
			{
				Title = "Wasabi is already running";
				Caption = "Please close any instance of Wasabi that is already running " +
				          "and try launching the app again.";

				SuggestionTextFragment1  = "Still has some questions? You can check out our";
				SuggestionTextFragment2  = "for some quick pointers.";

 				HideTraceTextBox = false;
			}
		}

		public SerializableException SerializedException { get; }

		public ICommand OpenGitHubRepoCommand { get; }

		public ICommand NextCommand { get; }

		public ICommand CancelCommand { get; }

		public string Caption { get; } = $"A problem has occurred and Wasabi is unable to continue.";

		public string Trace => $"{SerializedException.Message}{Environment.NewLine}" +
		                       $"{Environment.NewLine}{SerializedException.StackTrace}";

		public string Title { get; } = "Wasabi has crashed.";

		public string SuggestionTextFragment1 { get; } = "You can copy the above text and post an issue tracker in our";

		public string SuggestionTextFragment2 { get; } = "so we can take a closer look.";

		public bool HideTraceTextBox { get; }
	}
}