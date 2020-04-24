using System.Collections.Generic;

namespace WalletWasabi.Models
{
	public class ErrorDescriptors : List<ErrorDescriptor>, IErrorList
	{
		public static ErrorDescriptors Empty = Create();

		private ErrorDescriptors() : base()
		{
		}

		public bool HasErrors => Count > 0;

		public static ErrorDescriptors Create()
		{
			return new ErrorDescriptors();
		}

		void IErrorList.Add(ErrorSeverity severity, string error)
		{
			Add(new ErrorDescriptor(severity, error));
		}
	}
}
