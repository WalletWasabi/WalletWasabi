namespace WalletWasabi.Gui.Models
{

	public class ValidationMessageWrapper : IMessageWrapper
	{
		public object Message { get; }

		public ValidationMessageWrapper(object message)
		{
			Message = message;
		}
	}
}
