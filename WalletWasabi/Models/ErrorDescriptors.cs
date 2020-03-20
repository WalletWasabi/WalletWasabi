using System.Collections.Generic;

namespace WalletWasabi.Models
{
	public class ErrorDescriptors : List<ErrorDescriptor>
	{
		public static ErrorDescriptors Empty = new ErrorDescriptors();

		public ErrorDescriptors() : base()
		{
		}

		public ErrorDescriptors(params ErrorDescriptor[] errors)
		{
			AddRange(errors);
		}

		public bool HasErrors => Count > 0;
	}
}
