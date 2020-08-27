# Electrum Python Client Research

## What ElectrumX methods are used?

I ran this command to find this out:

```sh
ack "send_request\(\'"
```

Some of that stuff is for a plugin, so I gonna remove that, here's the methods (checkmarks means implemented):

### Headers

- [x] blockchain.block.header
- [x] blockchain.block.headers

### Fees

- [] blockchain.estimatefee
- [] blockchain.relayfee

### Scripthash (address)

- [] blockchain.scripthash.get_balance
- [] blockchain.scripthash.get_history
- [] blockchain.scripthash.listunspent

### Transactions

- [] blockchain.transaction.broadcast
- [] blockchain.transaction.get
- [] blockchain.transaction.get_merkle

### Mempool

- [] mempool.get_fee_histogram

### Server

- [] server.banner
- [] server.donation_address
- [] server.peers.subscribe
- [] server.peers.subscribe
- [] server.ping
- [] server.version
