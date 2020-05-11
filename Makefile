build:
	dotnet build LivianoWallet --framework netcoreapp2.1

build.ubuntu:
	dotnet build LivianoWallet --framework netcoreapp2.1 --configuration Debug --runtime ubuntu-x64

# Usage (run on debug): args="--configuration Debug" make run
run:
	dotnet run --project=LivianoWallet/Liviano.CLI --framework netcoreapp2.1 ${args}

# Usage (all tests):        make test
# Usage (full name):        test="Liviano.Tests.Liviano.HdOperationsTest.Bip84CompatibilityTest" make test
# Usage (method name):      test="Bip84CompatibilityTest" make test
# Usage (partial matching): test="Bip84" make test
# Usage (using 't'):        t="Bip84" make test
test:
	@if [ "${test}${t}" = "" ]; then\
		dotnet test LivianoWallet/Liviano.Tests --framework netcoreapp2.1;\
	fi
	@if [ "${test}" != "" ]; then\
		dotnet test LivianoWallet/Liviano.Tests --framework netcoreapp2.1 --filter "FullyQualifiedName~${test}";\
	fi
	@if [ "${t}" != "" ]; then\
		dotnet test LivianoWallet/Liviano.Tests --framework netcoreapp2.1 --filter "FullyQualifiedName~${t}";\
	fi

test_with_coverage:
	dotnet test LivianoWallet/Liviano.Tests --framework netcoreapp2.1 /p:CollectCoverage=true

publish_debug:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug
	mkdir -p bin/debug
	cp -R LivianoWallet/Liviano/bin/Debug/netcoreapp2.1/publish bin/debug/LivianoWallet
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
	cp -R LivianoWallet/Liviano/bin/Release/netcoreapp2.1/publish bin/release/LivianoWallet
	cp -R LivianoWallet/Liviano/bin/Release/netcoreapp2.1/win-x64/publish bin/release/LivianoWallet/win-x64
	cp -R LivianoWallet/Liviano/bin/Release/netcoreapp2.1/osx-x64/publish bin/release/LivianoWallet/osx-x64
	cp -R LivianoWallet/Liviano/bin/Release/netcoreapp2.1/ubuntu-x64/publish bin/release/LivianoWallet/ubuntu-x64
	cp -R LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/win-x64/publish bin/release/LivianoWalletCLI/win-x64
	cp -R LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/osx-x64/publish bin/release/LivianoWalletCLI/osx-x64
	cp -R LivianoWallet/Liviano.CLI/bin/Release/netcoreapp2.1/ubuntu-x64/publish bin/release/LivianoWalletCLI/ubuntu-x64

ubuntu_debug_build:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug --runtime ubuntu-x64
	mkdir -p bin/ubuntu_debug_build
	cp -R LivianoWallet/Liviano.CLI/bin/Debug/netcoreapp2.1/ubuntu-x64/publish bin/ubuntu_debug_build
	rm -rf ./liviano-cli
	ln -s bin/ubuntu_debug_build/publish/Liviano.CLI liviano-cli

osx_debug_build:
	dotnet publish LivianoWallet --framework netcoreapp2.1 --configuration Debug --runtime osx-x64
	mkdir -p bin/osx_debug_build
	cp -R LivianoWallet/Liviano.CLI/bin/Debug/netcoreapp2.1/osx-x64/publish bin/osx_debug_build
	rm -rf ./liviano-cli
	ln -s bin/osx_debug_build/publish/Liviano.CLI liviano-cli

clean:
	dotnet clean LivianoWallet --framework netcoreapp2.1
	rm -rf bin/*
	rm -rf obj/*
	rm -rf LivianoWallet/Liviano/obj/*
	rm -rf LivianoWallet/Liviano.CLI/obj/*
	rm -rf LivianoWallet/Liviano.Tests/obj/*
	rm -rf LivianoWallet/Liviano.Utilities/obj/*
	rm -rf LivianoWallet/Liviano/bin/*
	rm -rf LivianoWallet/Liviano.CLI/bin/*
	rm -rf LivianoWallet/Liviano.Tests/bin/*
	rm -rf LivianoWallet/Liviano.Utilities/bin/*
