//
// PriceViewModel.cs
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
using System.Runtime.Serialization;
using Liviano.Services.Models;
using ReactiveUI;

using Liviano.Services;

namespace Liviano.CLI.Gui.ViewModels
{
    [DataContract]
    public class PriceViewModel : ReactiveObject
    {
        PrecioEntity precio;
        public PrecioEntity Precio
        {
            get => precio;
            set => this.RaiseAndSetIfChanged(ref precio, value);
        }

        CurrencyEntity[] rates;
        public CurrencyEntity[] Rates
        {
            get => rates;
            set => this.RaiseAndSetIfChanged(ref rates, value);
        }

        readonly Price PriceService = new();

        public PriceViewModel()
        {
            PriceService.Start();

            PriceService
                .WhenAnyValue(service => service.Precio)
                .BindTo(this, vm => vm.Precio);

            PriceService
                .WhenAnyValue(service => service.Rates)
                .BindTo(this, vm => vm.Rates);
        }
    }
}
