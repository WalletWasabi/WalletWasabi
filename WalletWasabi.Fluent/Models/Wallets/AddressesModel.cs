using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AddressesModel
{
	private readonly KeyManager _keyManager;

	public AddressesModel(KeyManager keyManager)
	{
		_keyManager = keyManager;
	}

	public IEnumerable<IAddress> Unused => _keyManager
		.GetKeys()
		.Reverse()
		.Select(x => new Address(_keyManager, x))
		.Where(x => !x.IsUsed);
}
