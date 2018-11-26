using Liviano.Interfaces;
using NBitcoin;

namespace Liviano
{
    /// <inheritdoc cref="IScriptAddressReader"/>
    public class ScriptAddressReader : IScriptAddressReader
    {
        /// <inheritdoc cref="IScriptAddressReader.GetAddressFromScriptPubKey"/>
        public string GetAddressFromScriptPubKey(Network network, Script script)
        {
            var scriptTemplate = StandardScripts.GetTemplateFromScriptPubKey(script);

            var destinationAddress = string.Empty;

            switch (scriptTemplate.Type)
            {
                // This is what Satoshi used in the first transaction, is not secure :)
                case TxOutType.TX_PUBKEY:
                    PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);
                    destinationAddress = pubKey.GetAddress(network).ToString();
                    break;
                // Pay to PubKey hash is the regular, most common type of output.
                case TxOutType.TX_PUBKEYHASH:
                    destinationAddress = script.GetDestinationAddress(network).ToString();
                    break;
                case TxOutType.TX_SEGWIT:
                    destinationAddress = script.WitHash.GetAddress(network).ToString();
                    break;
                case TxOutType.TX_SCRIPTHASH:
                    // NOTE: @igorgue I made this for p2sh-p2wpkh, but is that what it can be used for?
                    // Since later you can notice there's a type "multisig"
                    destinationAddress = script.WitHash.ScriptPubKey.Hash.GetAddress(network).ToString();
                    break;
                case TxOutType.TX_NONSTANDARD: // Nonstandard transactions wont be processed
                case TxOutType.TX_MULTISIG: // NOTE: @igorgue: p2sh or traditional multisig?
                case TxOutType.TX_NULL_DATA: // Null data is for messages
                default:
                    break;
            }

            return destinationAddress;
        }
    }
}
