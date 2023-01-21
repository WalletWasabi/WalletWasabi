using System.Reactive;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Bridge;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public class ReceiveAddressViewModel : ReactiveObject
{
	private readonly ObservableAsPropertyHelper<bool[,]> _qrCode;

	public ReceiveAddressViewModel(IAddress address, IQrCodeGenerator qrCodeGenerator)
	{
		Address = address.HdPubKey.GetP2shOverP2wpkhAddress(address.Network).ToString();
		Labels = address.HdPubKey.Label;

		GenerateQrCodeCommand = ReactiveCommand.CreateFromObservable(() => qrCodeGenerator.Generate(Address));
		
		GenerateQrCodeCommand.Execute().Subscribe();
		_qrCode = GenerateQrCodeCommand.ToProperty(this, x => x.QrCode);
	}

	public bool[,] QrCode => _qrCode.Value;

	public ReactiveCommand<Unit, bool[,]> GenerateQrCodeCommand { get; }
	
	public string Address { get; }

	public SmartLabel Labels { get; }
}
