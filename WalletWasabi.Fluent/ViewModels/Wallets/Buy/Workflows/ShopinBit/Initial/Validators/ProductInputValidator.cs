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
		: base(workflowValidator, null, "Enter your location...", "Next")
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

			return null;
		}

		return null;
	}

	public override void OnActivation()
	{
		WorkflowValidator.Signal(true);
	}
}
