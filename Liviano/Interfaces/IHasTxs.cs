//
// IHasTxs.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;

using Liviano.Models;
using Liviano.Events;
using NBitcoin;

namespace Liviano.Interfaces
{
    public interface IHasTxs
    {
        /// <summary>
        /// Trigger when confirmations are updated
        /// </summary>
        event EventHandler<UpdatedConfirmationsArgs> OnUpdatedConfirmations;

        /// <summary>
        /// Adds transaction to this account
        /// </summary>
        /// <param name="tx">A <see cref="Tx"/> to be added</param>
        void AddTx(Tx tx);

        /// <summary>
        /// Updates a tx that matches tx.Id
        /// </summary>
        /// <param name="tx">A <see cref="Tx"/> to update</param>
        void UpdateTx(Tx tx);


        /// <summary>
        /// Removes a tx from the tx list
        /// </summary>
        /// <param name="tx">A <see cref="Tx"/> †o remove</param>
        void RemoveTx(Tx tx);

        /// <summary>
        /// Update transactions confirmations with height
        /// </summary>
        /// <param name="height">Height of the block</param>
        void UpdateConfirmations(long height);

        /// <summary>
        /// Update transactions CreatedAt attributes with the new header if needed
        /// </summary>
        /// <param name="header">BlockHeader</param>
        void UpdateCreatedAtWithHeader(BlockHeader header);
    }
}
