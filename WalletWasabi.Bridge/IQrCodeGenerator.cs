namespace WalletWasabi.Bridge;

public interface IQrCodeGenerator
{
	public IObservable<bool[,]> Generate(string data);
}
