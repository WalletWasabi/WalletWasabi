using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Legal;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalDocumentsViewModel : TextResourceViewModelBase
	{
		public LegalDocumentsViewModel(Global global) : base(global, "Legal Documents", filePath: global.LegalDocuments.FilePath)
		{
		}
	}
}
