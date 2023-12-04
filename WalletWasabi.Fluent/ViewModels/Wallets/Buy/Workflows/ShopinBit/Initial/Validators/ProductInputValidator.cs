using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ProductInputValidator : InputValidator
{
	private readonly InitialWorkflowRequest _initialWorkflowRequest;
	private readonly BuyAnythingClient.Product[] _productsEnum;

	[AutoNotify] private ObservableCollection<string> _products;
	[AutoNotify] private string? _product;

	public ProductInputValidator(
		IWorkflowValidator workflowValidator,
		InitialWorkflowRequest initialWorkflowRequest)
		: base(workflowValidator, null, "Enter your location...", "Send")
	{
		_initialWorkflowRequest = initialWorkflowRequest;

		_productsEnum = Enum.GetValues<BuyAnythingClient.Product>();

		_products = new ObservableCollection<string>(_productsEnum.Select(ProductHelper.GetDescription));
		_product = _products.FirstOrDefault();

		this.WhenAnyValue(x => x.Product)
			.Subscribe(_ => WorkflowValidator.Signal(IsValid()));
	}

	public override bool IsValid()
	{
		return !string.IsNullOrWhiteSpace(Product);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			var product = _productsEnum[_products.IndexOf(_product)];

			_initialWorkflowRequest.Product = product;

			return _product;
		}

		return null;
	}

	private bool _cabDisplayMessage = false;

	public override bool CanDisplayMessage()
	{
		return _cabDisplayMessage;
	}

	public override void OnActivation()
	{
		WorkflowValidator.Signal(true);
	}

	public override bool OnCompletion()
	{
		_cabDisplayMessage = true;
		return true;
	}
}
