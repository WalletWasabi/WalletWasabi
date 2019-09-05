using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalDocsViewModel : WasabiDocumentTabViewModel
	{
		public LegalDocsViewModel(Global global) : base(global, "About")
		{
		}

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel(Global));
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel(Global));
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel(Global));
		}
	}
}
