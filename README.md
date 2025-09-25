# CNet

CNet is a lightweight TCP and UDP networking library built in C# and designed for use in multiplayer games and real-time simulations. It was primarily built to be used in Unity, but it can also be used in any other .NET environments.


## Getting Started

### Current Features
- Built with .NET Standard 2.1
- Compatible with Unity
- Multithreaded and can support may connected users at once
- Connection handling (able to accept or deny clients)
- Disconnection handling
- Error handling
- Packet serialization/deserialization (can handle primitive data types as well as classes and structs)
- TCP and UDP support

### Future Features
- Adding security/cryptography
- Lowering packet size overhead (4 bytes to 2 bytes)

### Installation
1. Download latest from the [Releases](https://github.com/Monstroe/CNet/releases) page
2. Unzip folder
3. Drag and drop the 'CNet' directory into your development environment (or compile into DLL)

## Acknowledgements
- Heavily inspired by [LiteNetLib](https://github.com/RevenantX/LiteNetLib), thank you to [RevenantX](https://github.com/) for this amazing library
- Also took inspiration from [Unity Netcode](https://docs-multiplayer.unity3d.com/netcode/current/about/) and [Riptide Networking](https://github.com/RiptideNetworking/Riptide)
