using NBitcoin;
using System;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Services
{
	public class AddressWatchDog
	{
		private StringsRepository _repository;
		private bool _initialized = false;

		public AddressWatchDog(StringsRepository repository)
		{
			_repository = Guard.NotNull(nameof(repository), repository);
		}

		public async Task<bool> TryRegisterAsync(BitcoinAddress usedAdress)
		{
			if(_repository.TryAdd(usedAdress.ToString()))
			{
				await _repository.SaveChangesAsync();
				return true;
			}
			return false;
		}

		public bool IsAlreadyUsed(BitcoinAddress address)
		{
			return _repository.Exists(address.ToString());
		}

		public void Clear()
		{
			_repository.Clear();
		}
	}
}
