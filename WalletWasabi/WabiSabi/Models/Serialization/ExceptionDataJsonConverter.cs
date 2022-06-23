using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class ExceptionDataJsonConverter : GenericInterfaceJsonConverter<ExceptionData>
{
	public ExceptionDataJsonConverter() : base(new[] { typeof(InputBannedExceptionData), typeof(EmptyExceptionData), typeof(WrongPhaseExceptionData) })
	{
	}
}
