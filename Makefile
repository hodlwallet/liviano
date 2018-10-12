run:
	dotnet run --project=LivianoWallet/Liviano.CLI --framework netcoreapp2.1

build:
	dotnet build LivianoWallet --framework netcoreapp2.1

test:
	dotnet test LivianoWallet/Liviano.Tests --framework netcoreapp2.1 /p:CollectCoverage=true

publish_debug:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug
	mkdir -p bin/debug
	cp -R NBitcoin/NBitcoin/bin/Debug/netcoreapp2.1/publish bin/debug/NBitcoin
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.Wallet/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWallet
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWalletCLI

publish_release:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release
	mkdir -p bin/release
	cp -R NBitcoin/NBitcoin/bin/Release/netcoreapp2.1/publish bin/release/NBitcoin
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/publish bin/release/LivianoWallet
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/publish bin/release/LivianoWalletCLI

submodule_init:
	git submodule init
	git submodule update

submodule_update:
	git submodule update

clean:
	rm -rf bin/*

all: run
