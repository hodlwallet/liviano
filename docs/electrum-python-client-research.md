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

- [x] blockchain.estimatefee
- [x] blockchain.relayfee

### Scripthash (address)

- [x] blockchain.scripthash.get_balance
- [x] blockchain.scripthash.get_history
- [x] blockchain.scripthash.listunspent

### Transactions

- [x] blockchain.transaction.broadcast
- [x] blockchain.transaction.get
- [x] blockchain.transaction.get_merkle
- [x] blockchain.transaction.id_from_pos

### Mempool

- [x] mempool.get_fee_histogram

### Server

- [x] server.banner
- [x] server.donation_address
- [x] server.peers.subscribe
- [x] server.ping
- [x] server.version
