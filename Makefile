.PHONY: help build build.ubuntu run run.ubuntu run.ubuntu.debug test test.with.coverage test.watch.helper test.watch publish.debug publish.release  ubuntu.debug.build osx.debug.build clean clean.local

build:
	dotnet build --framework netcoreapp3.1 -property:GenerateFullPaths=true

build.ubuntu:
	dotnet build --framework netcoreapp3.1 --configuration Debug --runtime ubuntu-x64 -property:GenerateFullPaths=true

# Usage (run on debug): args="--configuration Debug" make run
run:
	dotnet run --project=Liviano.CLI --framework netcoreapp3.1 -property:GenerateFullPaths=true ${args}

run.ubuntu:
	make ubuntu.debug.build
	./liviano-cli ${args}

run.ubuntu.debug:
	make ubuntu.debug.build
	COMPlus_DebugWriteToStdErr=1 ./liviano-cli ${args}

# Usage (all tests):        make test
# Usage (full name):        test="Liviano.Tests.Liviano.HdOperationsTest.Bip84CompatibilityTest" make test
# Usage (method name):      test="Bip84CompatibilityTest" make test
# Usage (partial matching): test="Bip84" make test
# Usage (using 't'):        t="Bip84" make test
test:
	@if [ "${test}${t}" = "" ]; then\
		dotnet test Liviano.Tests --framework netcoreapp3.1;\
	fi
	@if [ "${test}" != "" ]; then\
		dotnet test Liviano.Tests --framework netcoreapp3.1 --filter "FullyQualifiedName~${test}";\
	fi
	@if [ "${t}" != "" ]; then\
		dotnet test Liviano.Tests --framework netcoreapp3.1 --filter "FullyQualifiedName~${t}";\
	fi

test.with.coverage:
	dotnet test Liviano.Tests --framework netcoreapp3.1 /p:CollectCoverage=true

test.watch.helper:
	echo "\e[7mRunning tests with coverage report\e[0m" && \
	echo "\e[7m==================================\e[0m" && \
	make -s test.with.coverage && \
	echo "\033[0;32mSuccess!!! All checks for Liviano are good!\033[0m" || \
	(echo "\033[0;31mFailure!!! Please check your tests or linters\033[0m"; exit 1)

test.watch:
	@if [ "${test}${t}" = "" ]; then\
		ack --csharp -f | entr -s "make -s test.watch.helper";\
	fi
	@if [ "${test}" != "" ]; then\
		ack --csharp -f | entr -s "test=${test} make -s test";\
	fi
	@if [ "${t}" != "" ]; then\
		ack --csharp -f | entr -s "test=${t} make -s test";\
	fi

publish.debug:
	dotnet publish --framework netcoreapp3.1 --configuration Debug -property:GenerateFullPaths=true
	mkdir -p bin/debug
	cp -R Liviano/bin/Debug/netcoreapp3.1/publish bin/debug/Liviano
	cp -R Liviano.CLI/bin/Debug/netcoreapp3.1/publish bin/debug/LivianoCLI

publish.release:
	dotnet publish --framework netcoreapp3.1 --configuration Release --runtime ubuntu-x64
	dotnet publish --framework netcoreapp3.1 --configuration Release --runtime win-x64
	dotnet publish --framework netcoreapp3.1 --configuration Release --runtime osx-x64
	mkdir -p bin/release/Liviano/win-x64
	mkdir -p bin/release/Liviano/osx-x64
	mkdir -p bin/release/Liviano/ubuntu-x64
	mkdir -p bin/release/Liviano/win-x64
	mkdir -p bin/release/LivianoCLI/osx-x64
	mkdir -p bin/release/LivianoCLI/ubuntu-x64
	cp -R Liviano/bin/Release/netcoreapp3.1/publish bin/release/Liviano
	cp -R Liviano/bin/Release/netcoreapp3.1/win-x64/publish bin/release/Liviano/win-x64
	cp -R Liviano/bin/Release/netcoreapp3.1/osx-x64/publish bin/release/Liviano/osx-x64
	cp -R Liviano/bin/Release/netcoreapp3.1/ubuntu-x64/publish bin/release/Liviano/ubuntu-x64
	cp -R Liviano.CLI/bin/Release/netcoreapp3.1/win-x64/publish bin/release/Liviano/win-x64
	cp -R Liviano.CLI/bin/Release/netcoreapp3.1/osx-x64/publish bin/release/LivianoCLI/osx-x64
	cp -R Liviano.CLI/bin/Release/netcoreapp3.1/ubuntu-x64/publish bin/release/LivianoCLI/ubuntu-x64

ubuntu.debug.build:
	dotnet publish --framework netcoreapp3.1 --configuration Debug --runtime ubuntu-x64 -property:GenerateFullPaths=true
	mkdir -p bin/ubuntu_debug_build
	cp -R Liviano.CLI/bin/Debug/netcoreapp3.1/ubuntu-x64/publish bin/ubuntu_debug_build
	rm -rf ./liviano-cli
	ln -s bin/ubuntu_debug_build/publish/Liviano.CLI liviano-cli

osx.debug.build:
	dotnet publish --framework netcoreapp3.1 --configuration Debug --runtime osx-x64 -property:GenerateFullPaths=true
	mkdir -p bin/osx_debug_build
	cp -R Liviano.CLI/bin/Debug/netcoreapp3.1/osx-x64/publish bin/osx_debug_build
	rm -rf ./liviano-cli
	ln -s bin/osx_debug_build/publish/Liviano.CLI liviano-cli

clean:
	dotnet clean --framework netcoreapp3.1
	rm -rf bin/*
	rm -rf obj/*
	rm -rf Liviano/obj/*
	rm -rf Liviano.CLI/obj/*
	rm -rf Liviano.Tests/obj/*
	rm -rf Liviano.Utilities/obj/*
	rm -rf Liviano/bin/*
	rm -rf Liviano.CLI/bin/*
	rm -rf Liviano.Tests/bin/*
	rm -rf Liviano.Utilities/bin/*

clean.local:
	make clean
	rm -rf wallets
	rm -rf liviano-cli
	rm -rf liviano.json

help:
	@echo "TOOD: Write makefile help."
