namespace WalletWasabi.Gui.Validation
{
	public interface IRegisterValidationMethod
	{
		void RegisterValidationMethod(string propertyName, ValidateMethod validateMethod);
	}
}
