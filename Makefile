build:
	dotnet build LivianoWallet --framework netcoreapp2.1

run:
	dotnet run --project=LivianoWallet/Liviano.CLI --framework netcoreapp2.1 ${args}

test:
	dotnet test LivianoWallet/Liviano.Tests --framework netcoreapp2.1 /p:CollectCoverage=true

publish_debug:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug
	mkdir -p bin/debug
	cp -R NBitcoin/NBitcoin/bin/Debug/netcoreapp2.1/publish bin/debug/NBitcoin
	cp -R LivianoWallet/Liviano.Wallet/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWallet
	cp -R LivianoWallet/Liviano.CLI/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWalletCLI

publish_release:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release --runtime ubuntu-x64
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release --runtime win-x64
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Release --runtime osx-x64
	mkdir -p bin/release/LivianoWallet/win-x64
	mkdir -p bin/release/LivianoWallet/osx-x64
	mkdir -p bin/release/LivianoWallet/ubuntu-x64
	mkdir -p bin/release/LivianoWalletCLI/win-x64
	mkdir -p bin/release/LivianoWalletCLI/osx-x64
	mkdir -p bin/release/LivianoWalletCLI/ubuntu-x64
	cp -R LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/publish bin/release/LivianoWallet
	cp -R LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/win-x64/publish bin/release/LivianoWallet/win-x64
	cp -R LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/osx-x64/publish bin/release/LivianoWallet/osx-x64
	cp -R LivianoWallet/Liviano.Wallet/bin/Release/netcoreapp2.1/ubuntu-x64/publish bin/release/LivianoWallet/ubuntu-x64
	cp -R LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/win-x64/publish bin/release/LivianoWalletCLI/win-x64
	cp -R LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/osx-x64/publish bin/release/LivianoWalletCLI/osx-x64
	cp -R LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/ubuntu-x64/publish bin/release/LivianoWalletCLI/ubuntu-x64

ubuntu_debug_build:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug --runtime ubuntu-x64
	mkdir -p bin/ubuntu_debug_build
	cp -R LivianoWallet/Liviano.CLI/bin/Debug/netcoreapp2.1/ubuntu-x64/publish bin/ubuntu_debug_build
	rm -rf ./liviano-cli
	ln -s bin/ubuntu_debug_build/publish/Liviano.CLI liviano-cli

submodule_init:
	git submodule init
	git submodule update

submodule_update:
	git submodule update

clean:
	rm -rf bin/*
