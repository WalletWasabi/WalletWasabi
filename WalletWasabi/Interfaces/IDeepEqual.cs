namespace WalletWasabi.Interfaces;

public interface IDeepEqual<T>
	where T : notnull
{
	bool DeepEquals(T other);
}
