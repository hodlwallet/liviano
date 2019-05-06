#!/bin/bash

sed -i.back "s/net461;net452;netstandard1.3;netstandard1.1;netcoreapp2.1;netstandard2.0/netstandard1.3;netstandard1.1;netcoreapp2.1;netstandard2.0/g" NBitcoin/NBitcoin/NBitcoin.csproj

