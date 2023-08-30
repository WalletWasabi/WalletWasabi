using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Gma.QrCodeNet.Encoding;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class QrCodeGenerator
{
	private readonly QrEncoder _encoder = new();

	public IObservable<bool[,]> Generate(string data)
	{
		return Observable.Start(() => _encoder.Encode(data).Matrix.InternalArray, DefaultScheduler.Instance);
	}
}
