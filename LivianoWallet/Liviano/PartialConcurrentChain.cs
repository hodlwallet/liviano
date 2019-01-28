using NBitcoin;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Liviano
{
    public class PartialConcurrentChain : ConcurrentChain
    {
        Dictionary<uint256, ChainedBlock> _BlocksById = new Dictionary<uint256, ChainedBlock>();
        ChainedBlock[] _BlocksByHeight = new ChainedBlock[0];
        ReaderWriterLock @lock = new ReaderWriterLock();
        int _CustomTipHeight;

        /// <summary>
        /// Creates a partial Concurrent chain which starts sycning from the customTipProvided.
        /// </summary>
        /// <param name="customTip"></param>
        public PartialConcurrentChain(ChainedBlock customTip)
        {
            _Tip = customTip;
            _CustomTipHeight = customTip.Height;
        }

        public void SetCustomTip(ChainedBlock newtip)
        {
            _Tip = newtip;
            _CustomTipHeight = newtip.Height;

        }
        public PartialConcurrentChain(Network network)
        {
            if (network != null)
            {
                var genesis = network.GetGenesis();
                SetTip(new ChainedBlock(genesis.Header, 0));
            }
        }

        public PartialConcurrentChain(Network network, ChainedBlock chainedBlock)
        {
            if (network != null)
            {
                var genesis = network.GetGenesis();
                SetTip(chainedBlock);
            }
        }

        public new void Load(BitcoinStream stream)
        {
            if (stream.Inner.Length == 0)
            {
                Log.Logger.Warning("Couldn't load chain because it was empty.");
                return;
            }
            var genesis = this.Genesis;
            using (@lock.LockWrite())
            {
                try
                {
                    while (true)
                    {
                        BlockHeader header = null;
                        int height = 0;
                        height = stream.ReadWrite(height);
                        header = stream.ReadWrite(header);

                        if (height == 0)
                        {
                            _BlocksByHeight = new ChainedBlock[0];
                            _BlocksById.Clear();
                            _Tip = null;
                            if (header != null && genesis != null && header.GetHash() != genesis.HashBlock)
                            {
                                throw new InvalidOperationException("Unexpected genesis block");
                            }
                            SetTipNoLock(new ChainedBlock(genesis?.Header ?? header, 0));
                        }
                        else if (_Tip.HashBlock == header.HashPrevBlock && !(header.IsNull && header.Nonce == 0))
                            SetTipNoLock(new ChainedBlock(header, height));
                        else
                            break;
                    }
                }
                catch (EndOfStreamException)
                {
                }
            }
        }

        public new byte[] ToBytes()
        {
            MemoryStream ms = new MemoryStream();
            WriteTo(ms);
            return ms.ToArray();
        }

        public new void WriteTo(BitcoinStream stream)
        {
            //Make sure chain isnt null and can enumerate??
            using (@lock.LockRead())
            {
                for (int i = _CustomTipHeight; i < Tip.Height + 1; i++)
                {
                    var block = GetBlockNoLock(i); // TODO: Add all blocks to a list first and insure none are null then write them to string.
                    if (block == null)
                    {
                        Log.Logger.Warning("Couldn't save chain because headers are not downloaded"); //For now return if null, dont save.
                        return;
                    }
                    stream.ReadWrite(block.Height);
                    stream.ReadWrite(block.Header);
                }
            }
        }

        public new PartialConcurrentChain Clone()
        {
            PartialConcurrentChain chain = new PartialConcurrentChain(_Tip);
            chain._Tip = _Tip;
            using (@lock.LockRead())
            {
                foreach (var kv in _BlocksById)
                {
                    chain._BlocksById.Add(kv.Key, kv.Value);
                }
                chain._BlocksByHeight = _BlocksByHeight.ToArray();
            }
            return chain;
        }

        /// <summary>
        /// Force a new tip for the chain
        /// </summary>
        /// <param name="pindex"></param>
        /// <returns>forking point</returns>
        public override ChainedBlock SetTip(ChainedBlock block)
        {
            using (@lock.LockWrite())
            {
                return SetTipNoLock(block);
            }
        }

        private ChainedBlock SetTipNoLock(ChainedBlock block)
        {
            int height = Tip == null ? -1 : Tip.Height;
            foreach (var orphaned in EnumerateThisToFork(block))
            {
                _BlocksById.Remove(orphaned.HashBlock);
                RemoveBlocksByHeight(orphaned.Height);
                height--;
            }
            var fork = GetBlockNoLock(height);
            foreach (var newBlock in block.EnumerateToGenesis()
                .TakeWhile(c => c != fork))
            {
                _BlocksById.AddOrReplace(newBlock.HashBlock, newBlock);
                AddOrReplaceBlocksByHeight(newBlock.Height, newBlock);
            }
            _Tip = block;
            return fork;
        }

        private IEnumerable<ChainedBlock> EnumerateThisToFork(ChainedBlock block)
        {
            if (_Tip == null)
                yield break;
            var tip = _Tip;
            while (true)
            {
                if (object.ReferenceEquals(null, block) || object.ReferenceEquals(null, tip))
                    throw new InvalidOperationException("No fork found between the two chains");
                if (tip.Height > block.Height)
                {
                    yield return tip;
                    tip = tip.Previous;
                }
                else if (tip.Height < block.Height)
                {
                    block = block.Previous;
                }
                else if (tip.Height == block.Height)
                {
                    if (tip.HashBlock == block.HashBlock)
                        break;
                    yield return tip;
                    block = block.Previous;
                    tip = tip.Previous;
                }
            }

        }



        #region IChain Members

        public override ChainedBlock GetBlock(uint256 id)
        {
            using (@lock.LockRead())
            {
                ChainedBlock result;
                _BlocksById.TryGetValue(id, out result);
                return result;
            }
        }

        private ChainedBlock GetBlockNoLock(int height)
        {
            ChainedBlock result;
            TryGetBlocksByHeight(height, out result);
            return result;
        }

        private bool TryGetBlocksByHeight(int height, out ChainedBlock result)
        {
            result = null;
            if (height >= _BlocksByHeight.Length || height < 0)
                return false;
            result = _BlocksByHeight[height];
            return result != null;
        }

        private void RemoveBlocksByHeight(int height)
        {
            if (height >= _BlocksByHeight.Length)
                return;
            _BlocksByHeight[height] = null;
        }

        public void AddOrReplaceBlocksByHeight(int height, ChainedBlock newBlock)
        {
            while (height >= _BlocksByHeight.Length)
            {
                Array.Resize(ref _BlocksByHeight, (int)((_BlocksByHeight.Length + 100) * 1.1));
            }
            _BlocksByHeight[height] = newBlock;
        }

        public override ChainedBlock GetBlock(int height)
        {
            using (@lock.LockRead())
            {
                return GetBlockNoLock(height);
            }
        }


        volatile ChainedBlock _Tip;
        public override ChainedBlock Tip
        {
            get
            {
                return _Tip;
            }
        }

        public override int Height
        {
            get
            {
                return Tip.Height;
            }
        }

        #endregion

        protected override IEnumerable<ChainedBlock> EnumerateFromStart()
        {
            int i = 0;
            ChainedBlock block = null;
            while (true)
            {
                using (@lock.LockRead())
                {
                    block = GetBlockNoLock(i);
                    if (block == null)
                        yield break;
                }
                yield return block;
                i++;
            }
        }

        public override string ToString()
        {
            return Tip == null ? "no tip" : Tip.Height.ToString();
        }



    }

    internal class ReaderWriterLock
    {
        ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();

        public IDisposable LockRead()
        {
            return new ActionDisposable(() => @lock.EnterReadLock(), () => @lock.ExitReadLock());
        }
        public IDisposable LockWrite()
        {
            return new ActionDisposable(() => @lock.EnterWriteLock(), () => @lock.ExitWriteLock());
        }

        internal bool TryLockWrite(out IDisposable locked)
        {
            locked = null;
            if (this.@lock.TryEnterWriteLock(0))
            {
                locked = new ActionDisposable(() =>
                {
                }, () => this.@lock.ExitWriteLock());
                return true;
            }
            return false;

        }
    }
    internal class ActionDisposable : IDisposable
    {
        Action onEnter, onLeave;
        public ActionDisposable(Action onEnter, Action onLeave)
        {
            this.onEnter = onEnter;
            this.onLeave = onLeave;
            onEnter();
        }

        #region IDisposable Members

        public void Dispose()
        {
            onLeave();
        }

        #endregion
    }
}
