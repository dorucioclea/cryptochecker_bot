using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetaTools.Models;

namespace MetaTools.Cryptos
{
    public class Crypto
    {

        public string name;

        public List<string> results = new List<string>();

        public virtual async Task check(string mnemonic)
        {

        }

    }
}
