using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.LegalDocs
{
	public class LegalIssuesViewModel : TextResourceViewModelBase
	{
		public LegalIssuesViewModel(Global global) : base(global, "Legal Issues", new Uri(Path.Combine(global.DataDir, "UpdateChecker", "LegalIssues.txt")))
		{
		}
	}
}
