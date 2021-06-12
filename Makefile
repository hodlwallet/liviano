.PHONY: help build build.ubuntu liviano run run.ubuntu run.ubuntu.debug run.osx run.osx.debug run.win run.win.debug test test.with.coverage test.watch.helper test.watch publish.debug publish.release  ubuntu.debug.build osx.debug.build clean clean.local

# Set default goal to help target
.DEFAULT_GOAL := build

# Set Net framework version to use with dotnet
# USING_NET_VERSION = --framework netappcore3.1
USING_NET_VERSION = --framework net5.0

# BIN_DIR_NET_VERSION = netcoreapp3.1
BIN_DIR_NET_VERSION = net5.0

## Builds Liviano project and its dependencies on Debug configuration mode for all Net target frameworks supported.
build:
	dotnet build --configuration Debug -property:GenerateFullPaths=true -consoleloggerparameters:NoSummary

## Publishes Liviano's client for the ubuntu platform.
liviano:
	make ubuntu.debug.build

## Runs Liviano's client from source code using a particular Net framework version. Usage: args="--help" make run
run:
	@if [ "${args}" = "" ]; then\
		dotnet run $(USING_NET_VERSION) --project Liviano.CLI -- --help;\
	fi
	@if [ "${args}" != "" ]; then\
		dotnet run $(USING_NET_VERSION) --project Liviano.CLI -- ${args};\
	fi

## Publishes Liviano's client for the Ubuntu platform and executes liviano using input arguments. Usage: args="--help" make run.ubuntu
run.ubuntu:
	make ubuntu.debug.build;
	./liviano-cli ${args}

## Publishes Liviano's client for the Ubuntu platform and executes liviano logging its output to stderr. Usage: args="--help" make run.ubuntu.debug
run.ubuntu.debug:
	make ubuntu.debug.build;
	COMPlus_DebugWriteToStdErr=1 ./liviano-cli ${args}

## Publishes Liviano's client for the macOS platform and executes liviano using input arguments. Usage: args="--help" make run.osx
run.osx:
	make osx.debug.build
	./liviano-cli ${args}

## Publishes Liviano's client for the macOS platform and executes liviano logging its output to stderr. Usage: args="--help" make run.osx.debug
run.osx.debug:
	make osx.debug.build
	COMPlus_DebugWriteToStdErr=1 ./liviano-cli ${args}

## Publishes Liviano's client for the Windows platform and executes liviano using input arguments. Usage: args="--help" make run.win
run.win:
	make win.debug.build
	Liviano.CLI\bin\Debug\$(BIN_DIR_NET_VERSION)\win-x64\publish\liviano-cli.exe ${args}

## Publishes Liviano's client for the Windows platform and executes liviano logging its output to stderr. Usage: args="--help" make run.win.debug
run.win.debug:
	make win.debug.build
	setx COMPlus_DebugWriteToStdErr=1
	Liviano.CLI\bin\Debug\$(BIN_DIR_NET_VERSION)\win-x64\publish\liviano-cli.exe ${args}
	setx COMPlus_DebugWriteToStdErr=0

# Usage (all tests):        make test
# Usage (full name):        test="Liviano.Tests.Liviano.HdOperationsTest.Bip84CompatibilityTest" make test
# Usage (method name):      test="Bip84CompatibilityTest" make test
# Usage (partial matching): test="Bip84" make test
# Usage (using 't'):        t="Bip84" make test
## Excutes all unit tests.
test:
	@if [ "${test}${t}" = "" ]; then\
		dotnet test $(USING_NET_VERSION) Liviano.Tests;\
	fi
	@if [ "${test}" != "" ]; then\
		dotnet test $(USING_NET_VERSION) Liviano.Tests --filter "FullyQualifiedName~${test}";\
	fi
	@if [ "${t}" != "" ]; then\
		dotnet test $(USING_NET_VERSION) Liviano.Tests --filter "FullyQualifiedName~${t}";\
	fi

# Executes all unit tests by generating coverage report.
test.with.coverage:
	dotnet test Liviano.Tests /p:CollectCoverage=true

## Executes all unit tests and generates code coverage report.
test.watch.helper:
	@echo "\e[7mRunning tests with code coverage report\e[0m"
	@echo "\e[7m==================================\e[0m"
	make -s test.with.coverage && \
	echo "\033[0;32mSuccess!!! All checks for Liviano are good!\033[0m" || \
	(echo "\033[0;31mFailure!!! Please check your tests or linters\033[0m"; exit 1)

## Keeps listening for any change into csharp files to rerun tests. Usage: [test|t]="Bip84CompatibilityTest" make test.watch
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

## Publishes Liviano using Debug configuration mode.
publish.debug:
	dotnet publish $(USING_NET_VERSION) --configuration Debug -property:GenerateFullPaths=true -consoleloggerparameters:NoSummary
	mkdir -p bin/debug
	cp -R Liviano/bin/Debug/$(BIN_DIR_NET_VERSION)/publish bin/debug/Liviano
	cp -R Liviano.CLI/bin/Debug/$(BIN_DIR_NET_VERSION)/publish bin/debug/LivianoCLI

## Publishes Liviano using Release configuration mode for the following target platforms: Windows, macOS and Ubuntu.
publish.release:
	dotnet publish $(USING_NET_VERSION) --configuration Release --runtime ubuntu-x64
	dotnet publish $(USING_NET_VERSION) --configuration Release --runtime win-x64
	dotnet publish $(USING_NET_VERSION) --configuration Release --runtime osx-x64
	mkdir -p bin/release/Liviano/win-x64
	mkdir -p bin/release/Liviano/osx-x64
	mkdir -p bin/release/Liviano/ubuntu-x64
	mkdir -p bin/release/Liviano/win-x64
	mkdir -p bin/release/LivianoCLI/osx-x64
	mkdir -p bin/release/LivianoCLI/ubuntu-x64
	cp -R Liviano/bin/Release/$(BIN_DIR_NET_VERSION)/publish bin/release/Liviano
	cp -R Liviano/bin/Release/$(BIN_DIR_NET_VERSION)/win-x64/publish bin/release/Liviano/win-x64
	cp -R Liviano/bin/Release/$(BIN_DIR_NET_VERSION)/osx-x64/publish bin/release/Liviano/osx-x64
	cp -R Liviano/bin/Release/$(BIN_DIR_NET_VERSION)/ubuntu-x64/publish bin/release/Liviano/ubuntu-x64
	cp -R Liviano.CLI/bin/Release/$(BIN_DIR_NET_VERSION)/win-x64/publish bin/release/Liviano/win-x64
	cp -R Liviano.CLI/bin/Release/$(BIN_DIR_NET_VERSION)/osx-x64/publish bin/release/LivianoCLI/osx-x64
	cp -R Liviano.CLI/bin/Release/$(BIN_DIR_NET_VERSION)/ubuntu-x64/publish bin/release/LivianoCLI/ubuntu-x64

## Publishes and prepares Liviano's symbolic link on Debug configuration mode for Ubuntu system.
ubuntu.debug.build:
	dotnet publish $(USING_NET_VERSION) --configuration Debug --runtime ubuntu-x64 -property:GenerateFullPaths=true
	mkdir -p bin/ubuntu_debug_build
	cp -R Liviano.CLI/bin/Debug/$(BIN_DIR_NET_VERSION)/ubuntu-x64/publish bin/ubuntu_debug_build
	rm -f ./liviano-cli
	ln -s bin/ubuntu_debug_build/publish/Liviano.CLI liviano-cli

## Publishes Liviano using Debug configuration mode for macOS system.
osx.debug.build:
	dotnet publish --framework net5.0 --configuration Debug --runtime osx-x64 -property:GenerateFullPaths=true
	mkdir -p bin/osx_debug_build
	cp -R Liviano.CLI/bin/Debug/$(BIN_DIR_NET_VERSION)/osx-x64/publish bin/osx_debug_build
	rm -rf ./liviano-cli
	ln -s bin/osx_debug_build/publish/Liviano.CLI liviano-cli

## Publishes Liviano using Debug configuration mode for Windows system.
win.debug.build:
	dotnet publish $(USING_NET_VERSION) --configuration Debug --runtime win-x64 -property:GenerateFullPaths=true

## Cleans the outputs created during the previous build. All intermediate (obj) and final output (bin) folders are removed. 
clean:
	dotnet clean
	rm -r bin/*
	rm -r obj/*
	rm -r Liviano/obj/*
	rm -r Liviano.CLI/obj/*
	rm -r Liviano.Tests/obj/*
	rm -r Liviano.Utilities/obj/*
	rm -r Liviano/bin/*
	rm -r Liviano.CLI/bin/*
	rm -r Liviano.Tests/bin/*
	rm -r Liviano.Utilities/bin/*

## Cleans Liviano's CLI local instalation.
clean.local:
	make clean
	rm liviano-cli
	rm liviano.json
	rm -rf wallets

## Count lines
cloc:
	cloc `git ls-files --recurse-submodules | grep -v .json | grep -v .js`

## Default target. Shows this help.
help:
	@printf "Usage:\n";
	@echo '  ${GREEN}[args="value"] ${YELLOW}make${RESET} ${GREEN}[target]${RESET}'
	@echo ''
	@echo 'Targets:'
	@awk '/^[a-zA-Z\.\-_0-9[:space:]]+:/ \
		{ \
		helpMessage = match(lastLine, /^## (.*)/); \
		if (helpMessage) { \
			helpCommand = substr($$1, 0, index($$1, ":")-1); \
			helpMessage = substr(lastLine, RSTART + 3, RLENGTH); \
			printf "  ${YELLOW}%-$(TARGET_MAX_CHAR_NUM)s${RESET} ${GREEN}%s${RESET}\n", helpCommand, helpMessage; \
		} \
	} \
	{ lastLine = $$0 }' $(MAKEFILE_LIST)
		

# COLORS
GREEN  := $(shell tput -Txterm setaf 2)
YELLOW := $(shell tput -Txterm setaf 3)
WHITE  := $(shell tput -Txterm setaf 7)
RESET  := $(shell tput -Txterm sgr0)

TARGET_MAX_CHAR_NUM = 20
