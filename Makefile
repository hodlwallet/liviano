run_cli:
	dotnet run --project=LivianoWallet/Liviano.CLI --framework netcoreapp2.1

build_solution:
	dotnet build LivianoWallet --framework netcoreapp2.1

publish_debug:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug
	mkdir -p bin/debug
	cp -R NBitcoin/NBitcoin/bin/Debug/netcoreapp2.1/publish bin/debug/NBitcoin
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/LivianoWallet/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWallet
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWalletCLI

publish_release:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release
	mkdir -p bin/release
	cp -R NBitcoin/NBitcoin/bin/Release/netcoreapp2.1/publish bin/release/NBitcoin
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/LivianoWallet/bin/Release/netcoreapp2.1/publish bin/release/LivianoWallet
	cp -R /home/igor/Code/LivianoWallet/LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/publish bin/release/LivianoWalletCLI

submodule_update:
	git submodule update

submodule_init:
	git submodule init
	git submodule update

clean:
	rm -rf bin/*

all: run_cli
