//
// AccountExtensions.cs
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
using System.Diagnostics;

using Liviano.Accounts;
using Liviano.Bips;
using Liviano.Interfaces;

namespace Liviano.Extensions
{
    public static class AccountExtensions
    {
        /// <summary>
        /// Helper function to allow the IAccount to be casted to e.g.: Bip141Account or any other account
        /// </summary>
        /// <param name="account">An <see cref="IAccount"/> that will be converted to something else</param>
        /// <returns></returns>
        public static IAccount CastToAccountType(this IAccount account)
        {
            return account.AccountType switch
            {
                "bip44" => (Bip44Account)account,
                "bip49" => (Bip49Account)account,
                "bip84" => (Bip84Account)account,
                "bip141" => (Bip141Account)account,
                "paper" => (PaperAccount)account,
                _ => throw new ArgumentException($"Invalid account type {account.AccountType}"),
            };
        }

        public static object TryGetProperty(this IAccount account, string name)
        {
            var prop = account.GetType().GetProperty(name);

            if (prop is null) return null;

            return prop.GetValue(account);
        }

        public static void TrySetProperty(this IAccount account, string name, object value)
        {
            var prop = account.GetType().GetProperty(name);

            if (prop is null) return;

            try
            {
                prop.SetValue(account, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrySetProperty] Unable to set property of {name} on account, incorrect value {value}");
                Debug.WriteLine($"[TrySetProperty] Error: {ex.Message}");
            }
        }

        public static string GetZPrv(this IAccount acc)
        {
            return acc.ExtKey.ToZPrv();
        }

        public static string GetZPub(this IAccount acc)
        {
            return acc.ExtPubKey.ToZPub();
        }

        public static string GetYPrv(this IAccount acc)
        {
            return acc.ExtKey.ToYPrv();
        }

        public static string GetYPub(this IAccount acc)
        {
            return acc.ExtPubKey.ToYPub();
        }
    }
}
