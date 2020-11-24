# Examples of ElectrumX commands

Ready to copy paste and look at what happens.

### get_merkle

```json
{"id": 0, "method": "blockchain.transaction.get_merkle", "params": ["aa526501657ba228b1bc3129b2580375c16c842052d0b5102c292446f2fff1a7", 645618]}
```

### id_from_pos

```json
{"id": 0, "method": "blockchain.transaction.id_from_pos", "params": [645618, 1]}
```

```json
{"id": 0, "method": "blockchain.transaction.id_from_pos", "params": [645618, 1, true]}
```

### fee histogram

```json
{"id": 0, "method": "mempool.get_fee_histogram", "params": []}
```

### server donation address

```json
{"id": 0, "method": "server.donation_address", "params": []}
```

### server ping

```json
{"id": 0, "method": "server.ping", "params": []}
```

### server banner

```json
{"id": 0, "method": "server.banner", "params": []}
```

```json
{"id": 0, "method": "blockchain.scripthash.subscribe", "params": ["c9ee729a1e8fb436fec6fb0a248fe405f1ed68c359b980f947663cdd32e17916"]}
```

### blockchain block headers

blockchain.block.headers(start_height, count, cp_height=0)

