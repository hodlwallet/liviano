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
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release --runtime ubuntu-x64
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release --runtime win-x64
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release --runtime osx-x64
	mkdir -p bin/release
	cp -R NBitcoin/NBitcoin/bin/Release/netcoreapp2.1/publish bin/release/NBitcoin
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/publish bin/release/LivianoWallet
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/win-x64/publish bin/release/LivianoWallet/win-x64
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/osx-x64/publish bin/release/LivianoWallet/osx-x64
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/ubuntu-x64/publish bin/release/LivianoWallet/ubuntu-x64
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/win-x64/publish bin/release/LivianoWalletCLI/win-x64
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/osx-x64/publish bin/release/LivianoWalletCLI/osx-x64
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/ubuntu-x64/publish bin/release/LivianoWalletCLI/ubuntu-x64

submodule_init:
	git submodule init
	git submodule update

submodule_update:
	git submodule update

clean:
	rm -rf bin/*

all: run
