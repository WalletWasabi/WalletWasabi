using System.IO;
using DevZH.UI;
using Newtonsoft.Json.Linq;

namespace HiddenWallet.GUI.UI
{
    public class WindowMain: Window
    {
		public Tab Tab = new Tab();

		public readonly PageGenerateWallet PageGenerateWallet = new PageGenerateWallet { AllowMargins = true };
		public readonly PageDecryptWallet PageDecryptWallet = new PageDecryptWallet { AllowMargins = true };
		public readonly PageRecoverWallet PageRecoverWallet = new PageRecoverWallet { AllowMargins = true };

		public readonly PageAliceWallet PageAliceWallet = new PageAliceWallet { AllowMargins = true };
	    public readonly PageBobWallet PageBobWallet = new PageBobWallet { AllowMargins = true };

		public WindowMain(string title, int width = 640, int height = 360, bool hasMenubar = true) : base(title, width, height, hasMenubar)
	    {
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			Child = Tab;

			bool walletExists = Shared.WalletClient.Exists();
			if(walletExists)
			{
				Tab.Children.Add(PageDecryptWallet);
				Tab.Children.Add(PageGenerateWallet);
				Tab.Children.Add(PageRecoverWallet);
			}
			else
			{
				Tab.Children.Add(PageGenerateWallet);
				Tab.Children.Add(PageRecoverWallet);
				Tab.Children.Add(PageDecryptWallet);
			}
		}
	}
}
