using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Gma.QrCodeNet.Encoding;

namespace WalletWasabi.Fluent.Models.UI;

public class QrGenerator : IQrCodeGenerator
{
	private readonly QrEncoder _encoder = new();

	public IObservable<bool[,]> Generate(string data)
	{
		return Observable.Start(() => _encoder.Encode(data).Matrix.InternalArray, DefaultScheduler.Instance);
	}
}
