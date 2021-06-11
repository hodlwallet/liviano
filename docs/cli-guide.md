# Liviano CLI Getting Started Guide

To show you how the CLI(`liviano-cli`) works, we gonna have some commands here, everything will be created on testnet. Note most of these are for Ubuntu or on Windows WSL.

## Create wallet

To create a wallet we need a seed, we use bip39 for mnemonics.

```sh
./liviano-cli --new-mnemonic
```

Would give you a 12 word mnemonic in English.

```
charge tomorrow sausage speed cloth front cement machine reduce body chicken olive
```

Now to create a a wallet we use that mnemonic:

```sh
./liviano-cli --new-wallet --mnemonic="charge tomorrow sausage speed cloth front cement machine reduce body chicken olive" --new-account-name="Testnet Wallet #1" --testnet
```

Will return the wallet id, this is irrelevant to use due to the wallet gets created and set as the active wallet on `liviano.json`.

```
e6d6a273-4cfb-4ce6-9698-bad1ce6ecdfe
```

This wallet will be a BIP84 account, with hd path: `m/84'/0'/1'` and bech32 addresses.

## Receive a transaction

With now a wallet, we can get an address:

```sh
./liviano-cli --get-address
```

```
tb1qyz8kjxetd2h82xytqam8xh7q0vssryqtvtarrr
```

We set our client to listen or start:

```sh
./liviano-cli --start
```

Output is something like this:

```log
[17:48:42 INF] Using wallet id: e6d6a273-4cfb-4ce6-9698-bad1ce6ecdfe
[17:48:44 INF] Press ESC to stop Liviano...
```

Now to receive we'll use https://testnet-faucet.mempool.co/ to send, with `0.001` which is the max we can send.

The notification will be sent to the client:

```log
[17:50:31 INF] Got notification on account 0 from address: tb1qyz8kjxetd2h82xytqam8xh7q0vssryqtvtarrr, notification status hash: 417da0670a21bb87ae7158cb81208a48342332c11015f1e0e5d3b5912de7655a
[17:50:40 INF] Found new transaction!: 8b6da0cba2a17bb60f449be3ee5586a7a5da8994cf78e76eb48cd3093b9076e6 from address: tb1qyz8kjxetd2h82xytqam8xh7q0vssryqtvtarrr
```

## Read the balance

```sh
./liviano-cli --balance
```

```log
[17:51:47 INF] Using wallet id: e6d6a273-4cfb-4ce6-9698-bad1ce6ecdfe
0 = 0.00100000
```

## Read coin control

```sh
./liviano-cli --coin-control
```

```log
Unspent Coins:
==============

8b6da0cba2a17bb60f449be3ee5586a7a5da8994cf78e76eb48cd3093b9076e6:0 Amount: 0.00100000
Total: 0.00100000

Spent Coins:
============

-- Empty --
Total: 0.00000000

Frozen Coins:
=============

-- Empty --
Total: 0.00000000

```

## More commands

`liviano-cli` is well documented under its help:

```sh
./liviano-cli --help
```
