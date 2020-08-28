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
