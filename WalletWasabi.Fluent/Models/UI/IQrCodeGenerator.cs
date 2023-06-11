namespace WalletWasabi.Fluent.Models.UI;

public interface IQrCodeGenerator
{
	IObservable<bool[,]> Generate(string data);
}
