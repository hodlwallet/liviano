using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;

using Liviano.Utilities;
using Liviano.Enums;
using System.Reflection;

namespace Liviano.Extensions
{
    public static class WalletExtensions
    {
        /// <summary>
        /// Determines whether the chain is downloaded and up to date.
        /// </summary>
        /// <param name="chain">The chain.</param>
        public static bool IsDownloaded(this ConcurrentChain chain)
        {
            if (chain.Tip == null) return false;

            return chain.Tip.Header.BlockTime.ToUnixTimeSeconds() > (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - TimeSpan.FromHours(1).TotalSeconds);
        }

        /// <summary>
        /// Gets the height of the first block created at or after this date.
        /// </summary>
        /// <param name="chain">The chain of blocks.</param>
        /// <param name="date">The date.</param>
        /// <returns>The height of the first block created after the date.</returns>
        public static int GetHeightAtTime(this ConcurrentChain chain, DateTime date)
        {
            int blockSyncStart = 0;
            int upperLimit = chain.Tip.Height;
            int lowerLimit = 0;
            bool found = false;
            while (!found)
            {
                int check = lowerLimit + (upperLimit - lowerLimit) / 2;
                DateTime blockTimeAtCheck = chain.GetBlock(check).Header.BlockTime.DateTime;

                if (blockTimeAtCheck > date)
                {
                    upperLimit = check;
                }
                else if (blockTimeAtCheck < date)
                {
                    lowerLimit = check;
                }
                else
                {
                    return check;
                }

                if (upperLimit - lowerLimit <= 1)
                {
                    blockSyncStart = upperLimit;
                    found = true;
                }
            }
            return blockSyncStart;
        }

        /// <summary>
        /// Generates a list of checkpoints in the blockchain.
        /// 
        /// Please check Workbooks/CheckpointsGenerator.workbook to see how this is generated.
        /// </summary>
        /// <returns>The checkpoints.</returns>
        /// <param name="network">Network.</param>
        public static List<ChainedBlock> GetCheckpoints(this Network network)

        {
            List<ChainedBlock> checkpoints = new List<ChainedBlock>();

            if (network == Network.Main)
            {
                // Checkpoints are every 20160 blocks, 0 is genesis
                checkpoints.Add(new ChainedBlock(new BlockHeader("010000002f82b87670845faadde3fedd0dbf5040db62ba2b25c23e2c8408c17400000000ed73df5023c8e8f477fb965fe4c3cbfe5ee34b7d8b56c3efa3f3f9c0b275c91318526a4affff001dd9539704", network), 20160));
                checkpoints.Add(new ChainedBlock(new BlockHeader("010000001a231097b6ab6279c80f24674a2c8ee5b9a848e1d45715ad89b6358100000000a822bafe6ed8600e3ffce6d61d10df1927eafe9bbf677cb44c4d209f143c6ba8db8c784b5746651cce222118", network), 40320));
                checkpoints.Add(new ChainedBlock(new BlockHeader("01000000934c2bd5a456180b404341a380d20f51d0862b38311deb4d9505450900000000299a1702e49cf69bc3d0a6eee27510cc3cca5a427e1d000b2ccaf907116aaf4822c6124c64ba0e1c5423c204", network), 60480));
                checkpoints.Add(new ChainedBlock(new BlockHeader("010000000e860de65c35a94d2e335be7d79aabb6e3ddf3918e6d65c61e5b230000000000e36abc2127229d3a94ae0e2067a0a75cab61629d5b2f01b927df43b6c0025a08976f954ced66471bfb11bb03", network), 80640));
                checkpoints.Add(new ChainedBlock(new BlockHeader("01000000ddf75090bebe04fd00bd5d54945a7e775ff21a012374e284fe5a0200000000007a71100da32b454f15e1863b6dda148c830f92c0e99806c10f69ac6392ea3cb9335a214dcb04041b24da04f8", network), 100800));
                checkpoints.Add(new ChainedBlock(new BlockHeader("010000003d03ef67e92310f1f1161fcf6e3631bcd25a93e5e422b5ac84a30000000000007096173096e73db173c4b21ae76bbbbf655ebb5bd9662e91da721144c54eeada8c79bb4dfa98001b5898b854", network), 120960));
                checkpoints.Add(new ChainedBlock(new BlockHeader("010000000a5d88ccd0c56b9bbe4c84acae7250a2d4dc5ba92f52783dd3070000000000002c92030e6281be57bec776b084dc316febfcb2487ab96ec71708af36531955e976af494e864a091a1dda09ed", network), 141120));
                checkpoints.Add(new ChainedBlock(new BlockHeader("01000000c48381c43b1d2ebd386c70971289aa69e974ff281fedd27f1b03000000000000fcec0145025b8ac811b486fc91f07f5a39a2170c2eee1066238dda4545af70b6c8df094fd7690d1a350999b5", network), 161280));
                checkpoints.Add(new ChainedBlock(new BlockHeader("01000000b807c2dec8b735f71bba13196f69dc26d2c75ea831862bd7b4040000000000009061c7196a009b9616b0cbc1a93e70c6334e1bd6efe27908850ab034c659fef29579be4f5f8b0a1a225d77a7", network), 181440));
                checkpoints.Add(new ChainedBlock(new BlockHeader("010000009d6f4e09d579c93015a83e9081fee83a5c8b1ba3c86516b61f0400000000000025399317bb5c7c4daefe8fe2c4dfac0cea7e4e85913cd667030377240cadfe93a4906b50087e051a84297df7", network), 201600));
                checkpoints.Add(new ChainedBlock(new BlockHeader("01000000747ccc507cb0be8b458daaaf94c168f48a555fda0995a84cb3030000000000001460f2f1855d75fc1be8aaf21b58b004fecaae04ffc681b9c6cfda641f6221d7367a21515c98041a236d0dff", network), 221760));
                checkpoints.Add(new ChainedBlock(new BlockHeader("02000000c1ff84e95f9a73d760b37e444056b74867fcd8a382e13cc110000000000000003998741bf1f6806b26bc8496f4efbccd52ca36b27b827c2dfb6f1f055e72c3a4d8765152cab016191b458076", network), 262080));
                checkpoints.Add(new ChainedBlock(new BlockHeader("02000000e03f401bd7d2484a323ecd4b6bf9945a1a35de61a442355102000000000000003eb2df22382e43f5b527c0a6b8b230bccb93947043b7f9357ad3f6591f2897498e6ae2522cf50119a2ca6735", network), 282240));
                checkpoints.Add(new ChainedBlock(new BlockHeader("020000006653331789442da38ff405a9f3807c7d3407a7e085b5e90e0000000000000000c5652bd21f27a0873515c5a538fc741ee61201d72413c1856e0986707ce3604dee798053422869186bda24f3", network), 302400));
                checkpoints.Add(new ChainedBlock(new BlockHeader("020000000f6af938320a7efb354df9da98f3e5c0a1de0715a2d107160000000000000000baba50a2116b65022b437a9c912c83d18c39a161d88d5d261011413c79570b735087245493b81f1869e5702b", network), 322560));
                checkpoints.Add(new ChainedBlock(new BlockHeader("02000000dc0ae15cad873162f27db2ff33d9fbe2193aa492e1e9d1050000000000000000339167c2bdf04f5aa7aa56b1ab8925619e7d851e2586d771c519402c640ed5e7dfd4d85487bb1818649c7a52", network), 342720));
                checkpoints.Add(new ChainedBlock(new BlockHeader("02000000063c2ef9016bf32a904eead62a7cc120cffa3cf243197206000000000000000018b6461deb0d4d9fc9c663a101474332aeaaf7750cc7b003fb71e0494e503d8c2e9d8f558e41161826b07eda", network), 362880));
                checkpoints.Add(new ChainedBlock(new BlockHeader("030000002e3de8a8992b5b347c14d7848cf7a24f2d629d745447110c000000000000000050bbb03bf0f75ec53f192bd77d76e4aafa1960e8015806dbd534197807fae80b5414435689b21018159391b9", network), 383040));
                checkpoints.Add(new ChainedBlock(new BlockHeader("04000000473ed7b7ef2fce828c318fd5e5868344a5356c9e93b6040400000000000000004409cae5b7b2f8f18ea55f558c9bfa7c5f4778a1a53172a48fc57e172d0ed3d264c5eb56c3a40618af9bc1c7", network), 403200));
                checkpoints.Add(new ChainedBlock(new BlockHeader("0000003021622c26a4e62cafa8e434c7e083f540bccc83923cb40605000000000000000048fac5ba22a2bd48ac0a191cd1811188d0d8a8dee205697d29a4142dce66efa082eba05728720518fdb374ce", network), 423360));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020ff45c783d09706e359dcc76083e15e51839e4ed531ffb30000000000000000008415970bdcc835293a110ee23879744b3e1538f519a3f6f9098da2da02a9d433c1f15158858b0318c6ddfe0e", network), 443520));
                checkpoints.Add(new ChainedBlock(new BlockHeader("0200002071dc10c7d0a87c167bb5bfc599965e05c733b29d42d70102000000000000000027415d37cf17407211074d169e766d1811c92f1f1046d2250807ac048f03199c515501593e1b021895212f47", network), 463680));
                checkpoints.Add(new ChainedBlock(new BlockHeader("000000207822040cc0474304b2268b8b6113314c7c1ed68a63add00000000000000000003c7050fddcb66e30805313783b12b12c6c62c0ebc323b92576d9480e0340b987c3f6af590b3101187665ce69", network), 483840));
                checkpoints.Add(new ChainedBlock(new BlockHeader("0000002023922ab77be55efd05cee12392a2b93793f2669fa30d720000000000000000003cb99a54e8356ef7a0bb85421ed8cc783734586782368d7cb0aed5de0cfe956162b1595a8c577e1749b786df", network), 504000));
                checkpoints.Add(new ChainedBlock(new BlockHeader("000000207866eb8d5115490c859c0df6173b966bc4c637226c4a0d0000000000000000009e4e76d4f89771e96a9cd3c1f75f4ae11ceac34e3884d606a2448e375967d21435bc065b495a4117421bd9ed", network), 524160));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020e2873bfe173976c6fca3b943a38ce846cc9d77a7b8610c00000000000000000006b4b391db2f5d19b471e05b07104a167770d1bf583e85c5987577732e92268602c6b55b91c1251778ec4a23", network), 544320));
                checkpoints.Add(new ChainedBlock(new BlockHeader("000000200cd536b3eb1cd9c028e081f1455006276b293467c3e5170000000000000000007bc1b27489db01c85d38a4bc6d2280611e9804f506d83ad00d2a33ebd663992f76c7725c505b2e174fb90f55", network), 564480));
            }
            else if (network == Network.TestNet)
            {
                // Checkpoints are every 100800 blocks, 0 is genesis
                checkpoints.Add(new ChainedBlock(new BlockHeader("020000001939e922692d67e9da0c512082b3caaebaf04fac89499b07f310af000000000022c4fd8dd050b04bac685e24a0d0d6d21101ad605bc9effed2a654016e903836b2640c5207d9001cda8a7fb7", network), 100800));
                checkpoints.Add(new ChainedBlock(new BlockHeader("02000000ee689e4dcdc3c7dac591b98e1e4dc83aae03ff9fb9d469d704a64c0100000000bfffaded2a67821eb5729b362d613747e898d08d6c83b5704646c26c13146f4c6de91353c02a601b3a817f87", network), 201600));
                checkpoints.Add(new ChainedBlock(new BlockHeader("02000000a5895a55e1291fc575f21f107adfb24f4adfba8a75deb716ed32000000000000c6cd6732a04c51f08b2af9ed3277ddf83f5cb97cf6e90b30dda26f1aa2f575245f5c44545e60331a024203fb", network), 302400));
                checkpoints.Add(new ChainedBlock(new BlockHeader("030000007ccf123dcf34b0c5627969c6eebfe4934fbd65244bf93109cd5282000000000065eecfb9cb61b373d7e8da19fa27ebab09c3a367369506763f173a5e40f8b64062dd57556c34021c218f078c", network), 403200));
                checkpoints.Add(new ChainedBlock(new BlockHeader("0300000069ea2a30f71b993f5692ed2d7feb266d8780d7a327743e79e6af010000000000619d9580aef06044392706715e917766e71d5a594b0299d08949209f563d4c4f8a25a65586ab001b831c3129", network), 504000));
                checkpoints.Add(new ChainedBlock(new BlockHeader("040000007bd55e0c495e4ffb54404450463a013445ddf6658291f2be62120000000000003db2bc32999d4d774c28b5d3602af74059c2c2b0afa4a15b540662d22be6379fe1dc4656202a091ae8ab9486", network), 604800));
                checkpoints.Add(new ChainedBlock(new BlockHeader("040000008c44837e5fc2c5d63450f23d311ae5b54daccc34c50ff52944dcfa00000000004fc1b5bb33da3ef5cb4914055674c932dca9f36646da6f78b65c023484a163f22da8c456f0ff0f1c9d56a0df", network), 705600));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020c83686174fd96b8bddc6a097680011e852694cf2a65d65c01b00000000000000199221fc7318c3a98f5259ef41478038767973d433e6c9c60c8252564221e876a771245780e2341a088d14a9", network), 806400));
                checkpoints.Add(new ChainedBlock(new BlockHeader("0000003094724e53bef465404b0bbecd5fab9df3945d89c8e21bee42afa10c0000000000794ab7fe567e15b75b3544f68f4d9ea15e5657a5b0204684a10feb329037753b5aed9957ffff001c51aa24d0", network), 907200));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020ecd0c218239a360d0d38dcea28a66060b6e01f1f6b9998879a60000000000000916fe1e5bba4812e2e1a59e9befef17d0067d1b50817f9bb52fe4c14093333a2171d0858c0cc521a59140b8f", network), 1008000));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020165c37bbe6f558e555540f3cf15a3ee5bf7826d6d85e5989c67a2400000000002398f3407d233713b96dfe3295de6c119fc064da6cef9ea50393a6a16065c1c3070fdb58f0ec091bb445a2e4", network), 1108800));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020e299ffd701caa20284b69e3b56ad1ed6699293914592f63dcc010000000000000409119b5141e2bbdb4a36a0e82ecf0373f29ce1019131e88c7ccf9343a23baf6a64d85980e173196b02af97", network), 1209600));
                checkpoints.Add(new ChainedBlock(new BlockHeader("0000002060d09b66acde67094a0199d102aa4de52945e47b97430b63fbdc0b0000000000d748daa1824c05ea3c305bed4ca5f0b58f1317624925b4c397f9fe154a0544b7fc24055bf0ff0f1b96af14f6", network), 1310400));
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020b2a21fd812e43a32f109bc31eeff0403205619bbae2990bfa500000000000000e24d20fb200bb20989faa0401dbccc49c8236f0ec1425b78868f46f3f176f0df0acd865b05c14a19e181d79f", network), 1411200));
                // NOTE exception to the 100800 blocks rule... on testnet there's a huge amount of blocks in these gaps for little time
                checkpoints.Add(new ChainedBlock(new BlockHeader("00000020d03eeff74eadfb6060591912d74d7d3c395bc7217dd5063f9100000000000000e30bd6356932222da86753d1bda3584b1b76c6229fc30625fdfad2637e9720fb3537df5ba866011a0b27a352", network), 1442000));
                // Back to 100800 blocks per checkpoint
                checkpoints.Add(new ChainedBlock(new BlockHeader("0080ff2f322d37e9186e4f46fc6516cca758b2ab9f238f85caac087a25020000000000003acc6d9a7905fa11304660bdcd37a39e02f990293c45a33b4b44cd0ee02783b7ca4dc05c28f7031a09d57910", network), 1512000));
            }

            return checkpoints;
        }

        public static ChainedBlock GetBIP39ActivationChainedBlock(this Network network)
        {
            BlockHeader blockHeader;
            int height;

            if (network == Network.Main)
            {
                blockHeader = new BlockHeader("020000005abd8e47d983fee4a20f83f93973d92f072a06c5bc6867640200000000000000b929390f399afa1cc074bb1219be0f6e10a18e338e8ba5b1acfadae86c59d8e01d5dc3520ca3031996821dc7", Network.Main);
                height = 277996;
            }
            else
            {
                blockHeader = new BlockHeader("02000000cc3b4f230127a925da29423cab8974a83b60a5212ce6fd9a30b682e7000000001d153b89315e7eebca2005582395b709a8cce47d626226d53db4a33cad513b8eaa5dc352ffff001d002654ae", Network.TestNet);
                height = 154932;
            }

            return new ChainedBlock(blockHeader, height);
        }

        public static BlockLocator GetDefaultBlockLocator(this Network network)
        {
            ChainedBlock bip39ActivationBlock = network.GetBIP39ActivationChainedBlock();
            BlockLocator defaultScanLocations = new BlockLocator();

            defaultScanLocations.Blocks.Add(network.GenesisHash);

            foreach (ChainedBlock checkpoint in network.GetCheckpoints())
            {
                // Genesis added already
                if (checkpoint.Height == 0)
                    continue;

                // Insert BIP 39 block
                if (checkpoint.Height > bip39ActivationBlock.Height)
                    defaultScanLocations.Blocks.Add(bip39ActivationBlock.HashBlock);

                defaultScanLocations.Blocks.Add(checkpoint.HashBlock);
            }

            return defaultScanLocations;
        }

        public static string InfoString(this Node node)
        {
            // Return a right padded string of 60 chars of the information of a node.
            return String.Format(
                "{0,65}",
                $"{node.RemoteSocketAddress.ToString()}:{node.RemoteSocketPort} ({node.PeerVersion.UserAgent}{node.PeerVersion.Version})"
             );
        }

        public static IEnumerable<Coin> GetCoins(this TxOutList me, Script script)
        {
            return me.AsCoins().Where(c => c.ScriptPubKey == script);
        }

        /// <summary>
        /// Based on transaction data, it decides if it's possible that native segwit script played a par in this transaction.
        /// </summary>
        public static bool PossiblyNativeSegWitInvolved(this Transaction me)
        {
            // We omit Guard, because it's performance critical in Wasabi.
            // We start with the inputs, because, this check is faster.
            // Note: by testing performance the order doesn't seem to affect the speed of loading the wallet.
            foreach (TxIn input in me.Inputs)
            {
                if (input.ScriptSig is null || input.ScriptSig == Script.Empty)
                {
                    return true;
                }
            }
            foreach (TxOut output in me.Outputs)
            {
                if (output.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
                {
                    return true;
                }
            }
            return false;
        }

        public static Money Percentage(this Money me, decimal perc)
        {
            return Money.Satoshis((me.Satoshi / 100m) * perc);
        }

        public static decimal ToUsd(this Money me, decimal btcExchangeRate)
        {
            return me.ToDecimal(MoneyUnit.BTC) * btcExchangeRate;
        }

        public static bool VerifyMessage(this BitcoinWitPubKeyAddress address, uint256 messageHash, byte[] signature)
        {
            PubKey pubKey = PubKey.RecoverCompact(messageHash, signature);
            return pubKey.WitHash == address.Hash;
        }

        /// <summary>
        /// If scriptpubkey is already present, just add the value.
        /// </summary>
        public static void AddWithOptimize(this TxOutList me, Money money, Script scriptPubKey)
        {
            TxOut found = me.FirstOrDefault(x => x.ScriptPubKey == scriptPubKey);
            if (found != null)
            {
                found.Value += money;
            }
            else
            {
                me.Add(money, scriptPubKey);
            }
        }

        /// <summary>
        /// If scriptpubkey is already present, just add the value.
        /// </summary>
        public static void AddWithOptimize(this TxOutList me, Money money, IDestination destination)
        {
            me.AddWithOptimize(money, destination.ScriptPubKey);
        }

        /// <summary>
        /// If scriptpubkey is already present, just add the value.
        /// </summary>
        public static void AddWithOptimize(this TxOutList me, TxOut txOut)
        {
            me.AddWithOptimize(txOut.Value, txOut.ScriptPubKey);
        }

        /// <summary>
        /// If scriptpubkey is already present, just add the value.
        /// </summary>
        public static void AddRangeWithOptimize(this TxOutList me, IEnumerable<TxOut> collection)
        {
            foreach (var txout in collection)
            {
                me.AddWithOptimize(txout);
            }
        }

        public static async Task StopAsync(this RPCClient rpc)
        {
            await rpc.SendCommandAsync("stop");
        }

        /// <summary>
        /// This guess the script type from the hd path, P2WSH doesn't have an hdpath,
        /// defaults to legacy, because bip32 addresses should be legacy, in the case of
        /// bip 141 then we  have a problem since it generates both kinds of address but
        /// not at the same time... We'll go back to this problem after.
        /// </summary>
        /// <param name="hdPath"></param>
        /// <returns></returns>
        public static ScriptTypes HdPathToScriptType(this string hdPath)
        {
            if (hdPath.StartsWith("m/44'"))
                return ScriptTypes.P2PKH;

            if (hdPath.StartsWith("m/45'"))
                return ScriptTypes.P2SH;

            if (hdPath.StartsWith("m/49'"))
                return ScriptTypes.P2SH_P2WPKH;

            if (hdPath.StartsWith("m/84'"))
                return ScriptTypes.P2WPKH;

            if (hdPath.StartsWith("m/0"))
                return ScriptTypes.UNKNOWN;

            return ScriptTypes.UNKNOWN;
        }

        public static bool IsBitcoinAddress(this string address, Network network = null)
        {
            if (network == null) network = Network.Main;

            try { BitcoinAddress.Create(address, network); return true; } catch { return false; }
        }

        public static Dictionary<string, object> ToDict(this object options)
        {
            Dictionary<string, object> kwargs = new Dictionary<string, object>();

            if (options is null) return kwargs;

            foreach (PropertyInfo prop in options.GetType().GetProperties())
            {
                string propName = prop.Name;
                var val = options.GetType().GetProperty(propName).GetValue(options, null);
                if (val != null)
                {
                    kwargs.Add(propName, val);
                }
                else
                {
                    kwargs.Add(propName, null);
                }
            }

            return kwargs;
        }

        public static byte[] ToScriptHash(this BitcoinAddress address)
        {
            return Hashes.SHA256(address.ScriptPubKey.ToBytes()).Reverse().ToArray();
        }

        public static string ToHex(this byte[] bytes)
        {
            return Encoders.Hex.EncodeData(bytes);
        }
    }
}
