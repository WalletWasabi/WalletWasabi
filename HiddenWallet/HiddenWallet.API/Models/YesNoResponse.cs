using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
    public class YesNoResponse : BaseResponse
    {
		public YesNoResponse() => Success = true;
		public bool Value { get; set; }
    }
}
