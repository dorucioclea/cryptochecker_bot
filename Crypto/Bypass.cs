using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Web3.Accounts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using MetaTools.Cryptos;

namespace MetaTools.Crypto
{
    internal class Bypass
    {

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

        //Sender
        public Account sender;
        public string words;

        //Hacked
        public Account hacked;

        public string contract_address, recipient;
        public BigInteger token_fee;
        public BigInteger transfer_fee;
        HexBigInteger gas, gas_price;
        TransferFunction token_transfer;

        public static List<Chain> chains = new List<Chain>()
        {
            new Chain() {name = "bsc", chain_id = 56, rpc = "http://65.21.198.186:8545/"},
            new Chain() {name = "polygon", chain_id = 137, rpc = "https://polygon-rpc.com"},
            new Chain() {name = "avax", chain_id = 43114, rpc = "https://api.avax.network/ext/bc/C/rpc"},
            new Chain() {name = "ftm", chain_id = 250, rpc = "https://rpc.ftm.tools"},
        };

        public Chain chain;

        public void create()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var wallet = new Wallet(mnemonic.ToString(), null);
            sender = wallet.GetAccount(0, chain.chain_id);
            words = mnemonic.ToString();
        }


        public async Task calculate_fees()
        {

            Web3 sender_web3 = new Web3(sender, chain.rpc);
            Web3 hacked_web3 = new Web3(hacked, chain.rpc);

            sender_web3.TransactionManager.UseLegacyAsDefault = true;
            hacked_web3.TransactionManager.UseLegacyAsDefault = true;

            var balanceOfFunctionMessage = new BalanceOfFunction() { Owner = hacked.Address };
            var balanceHandler = hacked_web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            var balTask = await balanceHandler.QueryAsync<BigInteger>(contract_address, balanceOfFunctionMessage);
            token_transfer = new TransferFunction()
            {
                To = recipient,
                TokenAmount = balTask,
                FromAddress = hacked.Address,
                GasPrice = await hacked_web3.Eth.GasPrice.SendRequestAsync() + Web3.Convert.ToWei(3, UnitConversion.EthUnit.Gwei),
            };
            var transferHandler = hacked_web3.Eth.GetContractTransactionHandler<TransferFunction>();
            token_transfer.Gas = await transferHandler.EstimateGasAsync(contract_address, token_transfer);
            token_fee = token_transfer.Gas.Value * (token_transfer.GasPrice.Value + Web3.Convert.ToWei(3, Nethereum.Util.UnitConversion.EthUnit.Gwei));

            gas = new HexBigInteger(21000);
            gas_price = await sender_web3.Eth.GasPrice.SendRequestAsync();
            transfer_fee = gas.Value * gas_price.Value;
        }

        public async Task withdraw()
        {
            Web3 sender_web3 = new Web3(sender, chain.rpc);
            Web3 hacked_web3 = new Web3(hacked, chain.rpc);
            sender_web3.TransactionManager.UseLegacyAsDefault = true;
            hacked_web3.TransactionManager.UseLegacyAsDefault = true;

            //Get token balance
            var balanceOfFunctionMessage = new BalanceOfFunction() { Owner = hacked.Address };
            var tokenHandler = hacked_web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            var token_amount = await tokenHandler.QueryAsync<BigInteger>(contract_address, balanceOfFunctionMessage);



            var transferHandler = hacked_web3.Eth.GetContractTransactionHandler<TransferFunction>();
            bool work = true;


            var input = new TransactionInput()
            {
                From = sender.Address,
                GasPrice = gas_price,
                Gas = gas,
                Value = new HexBigInteger(token_fee),
                To = hacked.Address,
            };
            var tx = await sender_web3.TransactionManager.SendTransactionAndWaitForReceiptAsync(input
            , null);

            Task.Run(async () =>
            {
                while (work)
                {
                    try
                    {
                        input.Nonce = await sender.NonceService.GetNextNonceAsync();
                        var tx = await sender_web3.TransactionManager.SendTransactionAsync(input);
                        await Task.Delay(2100);
                    }
                    catch 
                    {
                        break;
                    }
                }
            });

            Task.Run(async () =>
            {
                while (work)
                {
                    try
                    {
                        var token_tx = await transferHandler.SendRequestAsync(contract_address, token_transfer);
                    }
                    catch { }
                }
            });

            await Task.Delay(63000);
            work = false;

        }
    }
}
