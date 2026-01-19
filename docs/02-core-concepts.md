# Core Concepts

## Topic
A **topic** is a named logical stream of records. Producers publish records to a topic, and consumers read records from a topic. Topics are durable and backed by on-disk log segments.

## Partition
A **partition** is an ordered, append-only log within a topic. Each topic consists of one or more partitions, and each partition is stored independently on disk.

Key properties:
- Records within a partition are totally ordered by offset.
- A partition has exactly one active writer at a time.
- Different partitions of the same topic can be written and read in parallel.

## Offset
An **offset** is a monotonically increasing integer that uniquely identifies a record’s position within a partition.

Key properties:
- Offsets are assigned by the broker at append time.
- Offsets are immutable once assigned.
- Offsets are local to a partition (there is no global ordering across partitions).

## Consumer Position vs. Committed Offset

### Consumer Position
The **consumer position** is the offset of the *next record the consumer intends to read* from a partition.

In v1:
- Consumer position is **client-managed**.
- Each consumer tracks its own position locally (in memory or client-side storage).

### Committed Offset
A **committed offset** is a broker-managed, durable record of how far a consumer group has progressed in a partition.

In v1:
- Committed offsets are **not implemented**.
- The broker does not store or track consumer progress.

## Delivery Semantics

This broker provides **at-least-once delivery semantics** at the API level:
- Records are appended durably to disk before being visible to consumers.
- Consumers may receive the same record more than once if they retry fetches or restart without persisting their position.

Exactly-once semantics are explicitly out of scope for v1.

## Backpressure
**Backpressure** is the mechanism by which a system prevents producers or consumers from overwhelming the broker.

In v1, backpressure is handled in a minimal, request-scoped way:
- Producers are limited by maximum request size.
- Consumers control their own pace by choosing fetch sizes and polling frequency.
- The broker does not track slow or stalled consumers.

More advanced backpressure mechanisms (e.g., consumer lag tracking, quotas, or flow control) are deferred to v2.

This document prevents ambiguity.

> **NOTE:** Message Broker vs normal RPC
>
> Using a message broker has several advantages compared to direct RPC:
> - It can act as a buffer if the recipient is unavailable or overloaded, and thus improve system reliability.
> - It can automatically redeliver messages to a process that has crashed, and thus prevent messages from being lost.
> - It avoids the need for service discovery, since senders do not need to directly connect to the IP address of the recipient.
> - It allows the same message to be sent to several recipients.
> - It logically decouples the sender from the recipient (the sender just publishes messages and doesn’t care who consumes them).