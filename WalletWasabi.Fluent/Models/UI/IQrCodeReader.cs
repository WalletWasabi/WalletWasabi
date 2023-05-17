using Avalonia.Media.Imaging;

namespace WalletWasabi.Fluent.Models.UI;

public interface IQrCodeReader
{
	bool IsPlatformSupported { get; }

	IObservable<(string decoded, Bitmap bitmap)> Read();
}
