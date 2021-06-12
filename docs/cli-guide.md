# Liviano CLI Getting Started Guide

To show you how the CLI(`liviano-cli`) works, we gonna have some commands here, everything will be created on testnet.

## Compile client

We need to compile liviano to be able to use its `cli`:

```sh
make ubuntu.debug.build
```

Or on OSX:

```sh
make osx.debug.build
```

If you get to install make on windows (`choco install make` from a admin console), and dotnet you can try to fix our Windows command if needed.

```sh
make win.debug.build
```

On Windows you should add `Liviano.CLI/bin/Debug/net5.0/win-x64/publish` to your path, so you can call liviano like `liviano-cli.exe` on windows without the `./` the following commands have

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

## Spend the transcation

After we receive we can spend said transaction, we need to use the `--send` command for that. We currently have 1 UTXO with the value of `0.00100000` BTC, in order to send the minimum amount of fee (1 sat per Byte) we need to substract it from its total: `final_amount = total_in_utxo - minimum_fee`, `minimum_fee` is 141 sats for 1 sat per Byte, `final_amount = 0.00100000 - 0.00000141 = 0.00099859`, so `0.00099859` is our final amount.

We decided to return the money to the faucet, its address is this one: `mkHS9ne12qx9pS9VojpwU5xtRd4T7X7ZUt`.

Before doing this you can set liviano-cli to start as explained above to see its output, or you can choose to resync afterwards with `liviano-cli --resync` we will resync here.

```sh
./liviano-cli --send --amount=0.00099859 --fee=1 --addr=mkHS9ne12qx9pS9VojpwU5xtRd4T7X7ZUt
```

```log
[18:01:35 INF] Using wallet id: e6d6a273-4cfb-4ce6-9698-bad1ce6ecdfe
[18:01:39 INF] Successfully sent transaction, id: b74771882cff2da9e62a0e66020fa697075f56c26679c30a5fe0764c161e43c8
```

Now we resync to get an updated wallet and account.

```sh
./liviano-cli --resync
```

After about 2 minutes, our wallet will be synced with this output:

```log
[18:15:36 INF] Using wallet id: e6d6a273-4cfb-4ce6-9698-bad1ce6ecdfe
[18:15:37 INF] Sync started at 2021/06/11 06:15:37 PM!
[18:15:38 INF] Press ESC to stop Liviano...
[18:15:55 INF] Transaction found at height: 2004446!
[18:16:04 INF] Transaction found at height: 2004448!
[18:16:09 INF] Sync finished at 2021/06/11 06:16:09 PM. Total sync time: 31.68 seconds!
[18:16:09 INF] Transactions:
[18:16:09 INF] Id: b74771882cff2da9e62a0e66020fa697075f56c26679c30a5fe0764c161e43c8 Amount: -0.00099859 Height: 2004448 Confirmations: 0 Time: 06/12/2021 00:13:52 +00:00
[18:16:09 INF] Id: 8b6da0cba2a17bb60f449be3ee5586a7a5da8994cf78e76eb48cd3093b9076e6 Amount: +0.00100000 Height: 2004446 Confirmations: 2 Time: 06/11/2021 23:52:04 +00:00
[18:16:09 INF] Total: 0.00000000
[18:16:09 INF] Saving...
[18:16:09 INF] Closing process with pid: 151939
[18:16:09 INF] bye!
```

As you can see the balance is 0, so we sent all we had back to the faucet.

## More commands

`liviano-cli` is well documented under its help:

```sh
./liviano-cli --help
```
