using NBitcoin;

namespace Liviano
{
    public class WalletManager
    {
        public static Mnemonic NewMnemonic(string wordlist = "English", int wordCount = 24)
        {
            Wordlist bitcoinWordlist;
            WordCount bitcoinWordCount;

            switch (wordlist.ToLower())
            {
                case "english":
                bitcoinWordlist = Wordlist.English;
                break;
                case "spanish":
                bitcoinWordlist = Wordlist.Spanish;
                break;
                case "chinesesimplified":
                bitcoinWordlist = Wordlist.ChineseSimplified;
                break;
                case "chinesetraditional":
                bitcoinWordlist = Wordlist.ChineseTraditional;
                break;
                case "french":
                bitcoinWordlist = Wordlist.French;
                break;
                case "japanese":
                bitcoinWordlist = Wordlist.Japanese;
                break;
                case "portuguesebrazil":
                bitcoinWordlist = Wordlist.PortugueseBrazil;
                break;
                default:
                bitcoinWordlist = Wordlist.English;
                break;
            }

            switch (wordCount)
            {
                case 12:
                bitcoinWordCount = WordCount.Twelve;
                break;
                case 15:
                bitcoinWordCount = WordCount.Fifteen;
                break;
                case 18:
                bitcoinWordCount = WordCount.Eighteen;
                break;
                case 21:
                bitcoinWordCount = WordCount.TwentyOne;
                break;
                case 24:
                bitcoinWordCount = WordCount.TwentyFour;
                break;
                default:
                bitcoinWordCount = WordCount.TwentyFour;
                break;
            }

            return NewMnemonic(bitcoinWordlist, bitcoinWordCount);
        }

        public static Mnemonic NewMnemonic(Wordlist wordlist, WordCount wordCount)
        {
            return new Mnemonic(wordlist, wordCount);
        }
    }
}
