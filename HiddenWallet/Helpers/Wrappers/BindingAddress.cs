// http://stackoverflow.com/a/479419/2061103

namespace HiddenWallet.Helpers.Wrappers
{
    internal class BindingAddress
    {
        public BindingAddress(string s)
        {
            Address = s;
        }

        public string Address { get; set; }
    }
}