using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.Models
{
    public enum AddressType
    {
        Pay2PublicKeyHash,
        Pay2WitnessPublicKeyHash,
        Pay2ScriptHashOverPay2WitnessPublicKeyHash
    }
}
