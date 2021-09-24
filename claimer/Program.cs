using System;
using System.Numerics;
using System.Linq;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.VM;
using Utility = Neo.Network.RPC.Utility;

namespace claimer
{
    class Program
    {
        static void Main(string[] args)
        {
            string wif = Environment.GetEnvironmentVariable("WIF");
            string rpc = Environment.GetEnvironmentVariable("RPC");
            string agent = Environment.GetEnvironmentVariable("AGENT");
            ProtocolSettings settings = ProtocolSettings.Load("/dev/stdin");
            RpcClient client = new RpcClient(new Uri(rpc), null, null, settings);
            KeyPair keypair = Utility.GetKeyPair(wif);
            UInt160 contract = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            Signer[] signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = contract } };
            UInt160 target = UInt160.Parse(agent);
            byte[] script_balance = NativeContract.GAS.Hash.MakeScript("balanceOf", target);
            BigInteger balance = client.InvokeScriptAsync(script_balance).ConfigureAwait(false).GetAwaiter().GetResult().Stack[0].GetInteger();
            uint blocknum = client.GetBlockCountAsync().GetAwaiter().GetResult();
            byte[] script_unclaimed = NativeContract.NEO.Hash.MakeScript("unclaimedGas", target, blocknum);
            BigInteger unclaimed = client.InvokeScriptAsync(script_unclaimed).GetAwaiter().GetResult().Stack[0].GetInteger();
            TransactionManagerFactory factory = new TransactionManagerFactory(client);
            Console.WriteLine($"BALANCE: {balance}; UNCLAIMED: {unclaimed};");
            byte[] sync = target.MakeScript("sync");
            byte[] claim = target.MakeScript("claim");
            byte[] script = unclaimed > 1_00000000 ? sync.Concat(claim).ToArray() : balance > 1_00000000 ? claim : null;
            if (script is null) return;
            TransactionManager manager = factory.MakeTransactionAsync(script!, signers).GetAwaiter().GetResult();
            Transaction tx = manager.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            UInt256 txid = client.SendRawTransactionAsync(tx).GetAwaiter().GetResult();
            Console.WriteLine(txid);
        }
    }
}
