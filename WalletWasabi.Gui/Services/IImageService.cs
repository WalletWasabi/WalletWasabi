using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Services
{
	interface IImageService
	{
		Task SaveImageAsync(string path, object image);
	}
}
