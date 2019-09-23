using Avalonia.Media.Imaging;
using System;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Services
{
	class AvaloniaImageService : IImageService
	{
		public async Task SaveImageAsync(string path, object image)
		{
			if (image is Bitmap bmp)
			{
				await Task.Run(() => bmp.Save(path));
			}
			else
			{
				throw new InvalidOperationException("Image is not a valid image source.");
			}
		}
	}
}
