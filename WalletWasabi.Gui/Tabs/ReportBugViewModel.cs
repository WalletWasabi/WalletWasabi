using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using Avalonia;
using System;

namespace WalletWasabi.Gui.Tabs
{
	internal class ReportBugViewModel : WasabiDocumentTabViewModel
	{
		public ReportBugViewModel() : base("Report Bug")
		{
			CopyUrlCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					Application.Current.Clipboard.SetTextAsync(IssuesURL).GetAwaiter().GetResult();
				}
				catch (Exception)
				{
					// Apparently this exception sometimes happens randomly.
					// The MS controls just ignore it, so we'll do the same.
				}
			});
		}

		public string IssuesURL => "http://github.com/zkSNACKs/WalletWasabi/issues";

		public ReactiveCommand CopyUrlCommand { get; }
	}
}
