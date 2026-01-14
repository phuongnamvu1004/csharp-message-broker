# Core Concepts

Purpose: shared vocabulary and invariants.

Must define precisely:
- topic
- partition
- offset
- consumer position vs committed offset
- delivery semantics

This document prevents ambiguity.

> **NOTE:** Message Broker vs normal RPC
>
> Using a message broker has several advantages compared to direct RPC:
> - It can act as a buffer if the recipient is unavailable or overloaded, and thus improve system reliability.
> - It can automatically redeliver messages to a process that has crashed, and thus prevent messages from being lost.
> - It avoids the need for service discovery, since senders do not need to directly connect to the IP address of the recipient.
> - It allows the same message to be sent to several recipients.
> - It logically decouples the sender from the recipient (the sender just publishes messages and doesn’t care who consumes them).