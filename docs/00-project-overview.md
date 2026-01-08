# Project Overview

This project aims to implement a configurable message broker in .NET, encapsulating important features such as the Publish/Subscribe pattern, append-only log per partition, offsets, consumer groups, commit offsets, back-pressure and Config + CLI tooling. 

The ultimate goal of this project is to really understand the implementation of message brokers, why their provided features help with a large-scale system, as well as proving my dexterity in using the C# language/.NET framework outside web applications.

# Stretch Goals

> **NOTE:** Message Broker vs normal RPC
> 
> Using a message broker has several advantages compared to direct RPC:
> - It can act as a buffer if the recipient is unavailable or overloaded, and thus improve system reliability.
> - It can automatically redeliver messages to a process that has crashed, and thus prevent messages from being lost.
> - It avoids the need for service discovery, since senders do not need to directly connect to the IP address of the recipient.
> - It allows the same message to be sent to several recipients.
> - It logically decouples the sender from the recipient (the sender just publishes messages and doesn’t care who consumes them).

