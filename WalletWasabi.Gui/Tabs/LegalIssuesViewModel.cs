using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
    internal class LegalIssuesViewModel : WasabiDocumentTabViewModel
    {
        private static string legalIssuesText;

        public LegalIssuesViewModel(Global global) : base(global, "Legal Issues")
        {
            if (legalIssuesText == null)
            {
                var target = new Uri("resm:WalletWasabi.Gui.Assets.LegalIssues.txt");
                var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();

                using (var stream = assetLocator.Open(target))
                using (var reader = new StreamReader(stream))
                {
                    legalIssuesText = reader.ReadToEnd();
                }
            }
        }

        public string LegalIssues => legalIssuesText;
    }
}