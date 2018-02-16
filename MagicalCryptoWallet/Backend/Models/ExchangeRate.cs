using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models
{
    public class ExchangeRate
	{
		[Required]
		public string Ticker { get; set; }
		[Required]
		public decimal Rate { get; set; } 
    }
}
