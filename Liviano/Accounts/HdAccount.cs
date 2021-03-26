//
// HdAccount.cs
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

namespace Liviano.Accounts
{
    public abstract class HdAccount : BaseAccount
    {
        /// <summary>
        /// This is the amount of address to generate
        /// </summary>
        int gapLimit = 20;
        public override int GapLimit
        {
            get => gapLimit;
            set
            {
                if (value < 0) throw new ArgumentException($"Invalid value {value}");

                gapLimit = value;
            }
        }

        /// <summary>
        /// Change addresses count
        /// </summary>
        /// <value></value>
        int internalAddressesCount = 0;
        public override int InternalAddressesCount
        {
            get => internalAddressesCount;
            set => internalAddressesCount = value >= GapLimit + InternalAddressesIndex ? InternalAddressesIndex : value;
        }

        /// <summary>
        /// Change addresess index
        /// </summary>
        /// <value></value>
        int internalAddressesIndex = 0;
        public override int InternalAddressesIndex
        {
            get => internalAddressesIndex;
            set => internalAddressesIndex = value;
        }

        /// <summary>
        /// Receive addresess count
        /// </summary>
        /// <value></value>
        int externalAddressesCount = 0;
        public override int ExternalAddressesCount
        {
            get => externalAddressesCount;
            set => externalAddressesCount = value >= GapLimit + ExternalAddressesIndex ? ExternalAddressesIndex : value;
        }

        /// <summary>
        /// Receive addresess index
        /// </summary>
        /// <value></value>
        int externalAddressesIndex = 0;
        public override int ExternalAddressesIndex
        {
            get => externalAddressesIndex;
            set => externalAddressesIndex = value;
        }
    }
}
