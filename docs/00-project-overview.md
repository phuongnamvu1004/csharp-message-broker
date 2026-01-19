# Project Overview
This project aims to implement a configurable message broker in .NET, encapsulating important features such as the Publish/Subscribe pattern, append-only log per partition, offsets, consumer groups, commit offsets, back-pressure and Config + CLI tooling. 

The ultimate goal of this project is to really understand the implementation of message brokers, why their provided features help with a large-scale system, as well as proving my dexterity in using the C# language/.NET framework outside web applications.

---

# V1 Scope (Standalone Consumers)
This project’s first shippable version (v1) focuses on a **durable, log-based broker** with **standalone consumers** (Kafka “without a group”). Multiple consumers can connect concurrently, but the broker does not coordinate them.

## V1 Goals
- A broker process that accepts connections from clients.
- Clients can publish to a topic partition; the broker appends records to an on-disk log.
- Clients can fetch records from a topic partition starting at a specific offset (stateless fetch).
- A CLI tool to publish and fetch messages (and basic metadata commands like listing partitions).
- Basic durability and crash recovery (append-only segments + checkpoint + truncate-to-last-valid).

## V1 Non-Goals (to prevent scope creep)
- No consumer groups
- No rebalancing
- No broker-managed committed offsets
- Each consumer decides which partitions to read and tracks its own offsets

---

# V2 Goals (Next Milestone)
These features are intentionally deferred until after v1 is stable:
- Consumer groups
- Consumer offsets (committed offsets)
- Back-pressure

---

# Configuration
The broker will be configurable via a YAML file. The following configuration options will be supported:
- Port number
- Log directory
- Max connections 
- Max message sizes
- Buffer sizes
- logging level
- Topic auto-create: on/off

Example configuration file `config.yaml`:
```yaml
port: 1234
logDir: /var/log/broker
maxConnections: 1000
maxMessageSize: 1024
bufferSizes:
  - 1024
  - 2048
logLevel: info
```

---

# CLI
As a core part of this project, I will implement a CLI tool that allows users to interact with the broker.

## Usage
The CLI will be a simple command-line tool that allows users to:
- Start the broker process
- Publish messages to a topic
- Subscribe to a topic and receive messages
- Example sessions:
    - Publish messages to a topic and subscribe to it in a separate terminal window.
    - Publish messages to a topic and subscribe to it in a separate terminal window, then kill the broker process.

## Commands
| broker    | command                                    | description                     |
|-----------|--------------------------------------------|---------------------------------|
| start     | `broker start --config config.yaml`        | Starts the broker process.      |
| publish   | `broker pub --topic orders --message "hi"` | Publishes a message to a topic. |
| subscribe | `broker sub --topic orders`                | Subscribes to a topic.          |



