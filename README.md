# LivianoWallet

Liviano is a proof of concept light wallet framework for Bitcoin written in C# with NBitcoin. It includes a CLI and a Daemon. Liviano is ideal for running your own node to spin off your own mobile wallet!

## Requirements

- `.NET Core 2.1`: https://docs.microsoft.com/en-us/dotnet/core/

## Supported Platforms

We use Xamarin and NBitcoin and support .NET Standard 2.0 which makes this library compatible with the following platforms:

- Windows (.NET Framework)
- Mac (Xamarin, Mono Mac)
- Linux (.NET Core 2.0 or 2.1)
- iOS (Xamarin)
- Android (Xamarin)

## Pre-Install

We need NBitcoin we use the latest on their `master` branch.

```
make submodule_init
```

## Build Instructions

```
make build
```

## Run Tests

```
make test
```

## Make Commands:

| Command | Description |
| --- | --- |
| `run` | run `Liviano.CLI` project, use `args="--version" make run` to run with argument `--version`  |
| `build` | builds `LivianoWallet` solution |
| `test` | runs the test project `Liviano.Tests` |
| `publish[_debug,_release]` | build to publish `LivianoWallet` solution |
| `submodule[_init,_update]` | handles `NBitcoin` submodule |
| `clean` | removes executables from `bin` |
| `[ubuntu,osx]_debug_build` | creates a debug build for an specific OS installed in ./liviano-cli |
