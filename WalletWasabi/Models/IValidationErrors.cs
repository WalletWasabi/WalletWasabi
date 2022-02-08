namespace WalletWasabi.Models;

public interface IValidationErrors
{
	void Add(ErrorSeverity severity, string error);
}
