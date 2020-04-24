namespace WalletWasabi.Models
{
	public interface IErrorList
	{
		void Add(ErrorSeverity severity, string error);
	}
}
