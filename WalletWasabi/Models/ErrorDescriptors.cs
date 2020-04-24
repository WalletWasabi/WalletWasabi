using System.Collections.Generic;

namespace WalletWasabi.Models
{
	public interface IErrorList
	{
		void Add(ErrorSeverity severity, string error);
	}

	public class ErrorDescriptors : List<ErrorDescriptor>, IErrorList
	{
		public static ErrorDescriptors Create ()
		{
			return new ErrorDescriptors();
		}

		public static ErrorDescriptors Empty = Create();

		private ErrorDescriptors() : base()
		{
		}

		public bool HasErrors => Count > 0;

		void IErrorList.Add(ErrorSeverity severity, string error)
		{
			Add(new ErrorDescriptor(severity, error));
		}
	}
}
