using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Container
{
	public interface IProgramLifecycle
	{
		Task InitializeNoWalletAsync();

		Task DisposeAsync();
	}
}
