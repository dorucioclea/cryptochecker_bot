using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Util;
using Nethereum.Web3;
using Solnet.Extensions;
using Solnet.Rpc;
using YourEasyBot;

namespace MetaTools.Cryptos
{
    public class Solana : Crypto
    {

        int deep = 5;

        public Solana()
        {
            this.name = "solana";
        }

        private class Token
        {
            public string name, value;
        }

        public override async Task check(string mnemonic)
        {
            try
            {
                var wallet = new Solnet.Wallet.Wallet(mnemonic);
                for (int i = 0; i <= deep; i++)
                {
                    var account = wallet.GetAccount(i);
                    string address = account.PublicKey;
                    string privateKey = account.PrivateKey;

                    var tokens = await getTokensAsync(address);
                    var balance = await getBalance(address);
                    if (tokens.Count != 0 || balance != 0)
                    {
                        StringBuilder result = new StringBuilder($"============\n| {mnemonic}\n| {privateKey}\n| {address}\n| SOL: {balance}\n");



                        foreach (Token t in tokens)
                        {
                            result.Append($"\n| {t.value} {t.name}");
                        }
                        results.Add(result.ToString());
                    }
                }
            }
            catch
            {
            }
        }

        private static async Task<BigDecimal> getBalance(string address)
        {
            while (true)
            {
                var request_balance = await Utils.solanaRPC.GetBalanceAsync(address);
                if (request_balance.WasSuccessful)
                {
                    return Web3.Convert.FromWeiToBigDecimal(request_balance.Result.Value, 9);
                }
            }
        }


        private static async Task<List<Token>> getTokensAsync(string address)
        {
            while (true)
            {
                List<Token> tokens = new List<Token>();

                var tokens_list = await Utils.solanaRPC.GetTokenAccountsByOwnerAsync(address, tokenProgramId: "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA");

                if (tokens_list.WasSuccessful)
                {
                    foreach (var t in tokens_list.Result.Value)
                    {
                        if (t.Account.Data.Parsed.Info.TokenAmount.AmountUlong != 0)
                        {
                            var token = Utils.resolver.Resolve(t.Account.Data.Parsed.Info.Mint);
                            tokens.Add(new Token() { name = token.Symbol, value = t.Account.Data.Parsed.Info.TokenAmount.UiAmountString });
                        }
                    }
                    return tokens;
                }
            }

        }
    }
}
