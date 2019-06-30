using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class TermsAndConditionsViewModel : WasabiDocumentTabViewModel
	{
		public string TermsAndConditions { get; }

		public TermsAndConditionsViewModel(Global global) : base(global, "Terms and Conditions")
		{
			var target = new Uri("resm:WalletWasabi.Gui.Assets.TermsAndConditions.txt");
			var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();

			using (var stream = assetLocator.Open(target))
			using (var reader = new StreamReader(stream))
			{
				TermsAndConditions = reader.ReadToEnd();
			}
		}
	}
}
