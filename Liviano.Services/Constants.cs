//
// Constants.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2022 HODL Wallet
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
namespace Liviano.Services
{
    public static class Constants
    {
        public const string MEMPOOL_SPACE_API = "https://mempool.space/api/v1";
        public const string PRECIO_API = "https://precio.bitstop.co/";

#if DEBUG
        public const int MEMPOOL_SPACE_2H_STATS_INTERVAL_MS = 5_000;
        public const int FEE_ESTIMATOR_INTERVAL_MS = 5_000;
        public const int PRECIO_INTERVAL_MS = 5_000;
#else
        public const int MEMPOOL_SPACE_2H_STATS_INTERVAL_MS = 10_000;
        public const int FEE_ESTIMATOR_INTERVAL_MS = 30_000;
        public const int PRECIO_INTERVAL_MS = 10_000;
#endif
    }
}
