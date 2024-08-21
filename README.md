# CNet

CNet is a lightweight TCP and UDP networking library built in C# and designed for use in multiplayer games and realtime simulations. It was primarily built to be used in Unity, but it can also be used in any other .NET environments. This project is still actively under development, see the future additions below.

### High-Level Unity API COMING SOON!

## Getting Started

### Current Features
- Build with .NET Standard 2.1
- Can be used with Unity
- Simple connection handling (able to accept or deny clients)
- Simple disconnection handling
- Error handling
- Packet serialization/deserialization (can handle primitive data types as well as classes and structs)
- TCP and UDP support

### Future Features
- Adding security/cryptography
- Lowering packet size overhead (4 bytes to 3 bytes)
- Peer to peer connections (UDP hole punching)

### Installation
1. Download latest from releases page
2. Unzip folder
3. Drag and drop the 'CNet' directory into your development environment OR compile into DLL

## Acknowledgements
- Official networking library of [Hidden Roll](https://hiddenroll.com/)
- Heavily inspired by [LiteNetLib](https://github.com/RevenantX/LiteNetLib), thank you to [RevenantX](https://github.com/) for this amazing library
- Also took inspiration from [Unity Netcode](https://docs-multiplayer.unity3d.com/netcode/current/about/) and [Riptide Networking](https://github.com/RiptideNetworking/Riptide)