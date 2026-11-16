using NBitcoin;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.Wallets;

/// <summary>
/// Builder class for <see cref="WalletWasabi.Wallets.Wallet"/>.
/// </summary>
public class WalletBuilder
{
	private Network _network = Network.Main;
	private KeyManager? _keyManager;
	private EventBus? _eventBus;
	private ServiceConfiguration _serviceConfiguration = new(Money.Satoshis(1000));
	private MempoolService? _mempoolService;
	private FilterHeaderChain? _filterHeaderChain;

	/// <remarks>Provider that returns no blocks.</remarks>
	private BlockProvider _blockProvider = (blockHash, cancellationToken) => Task.FromResult<Block?>(null);

	public WalletBuilder WithNetwork(Network network)
	{
		_network = network;
		return this;
	}

	public WalletBuilder WithKeyManager(KeyManager keyManager)
	{
		_keyManager = keyManager;
		return this;
	}

	public WalletBuilder WithEventBus(EventBus eventBus)
	{
		_eventBus = eventBus;
		return this;
	}

	public WalletBuilder WithServiceConfiguration(ServiceConfiguration serviceConfiguration)
	{
		_serviceConfiguration = serviceConfiguration;
		return this;
	}

	public WalletBuilder WithBlockProvider(BlockProvider blockProvider)
	{
		_blockProvider = blockProvider;
		return this;
	}

	public WalletWasabi.Wallets.Wallet Build()
	{
		_eventBus ??= new EventBus();
		_mempoolService ??= new MempoolService(_eventBus);
		_filterHeaderChain ??= new FilterHeaderChain();
		_keyManager ??= ServiceFactory.CreateKeyManager("");

		var factory = WalletWasabi.Wallets.Wallet.CreateFactory(
			_network,
			filterManager: null!,
			transactionStore: null!,
			filterHeaderChain: _filterHeaderChain,
			mempoolService: _mempoolService,
			_serviceConfiguration,
			blockProvider: _blockProvider,
			_eventBus,
			cpfpInfoProvider: null!);

		return factory(_keyManager);
	}
}
