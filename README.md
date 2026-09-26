# Auralis

Auralis is an early-stage, modular C# game engine and runtime framework targeting **.NET 10**. The project is organized as a solution of independent libraries spanning core utilities, mathematics, an entity-based engine runtime, and asset-pipeline tools.

## Solution Layout

The repository uses the new `.slnx` solution format ([`Auralis.slnx`](Auralis.slnx)), with folders reserved for planned subsystems (animation, audio, graphics, physics, rendering, scene, simulation, editor, platform).

```
Auralis/
├── Auralis.slnx                      # Solution file (.NET 10 slnx format)
├── src/
│   ├── Auralis.Core/
│   │   ├── Auralis.Core/             # Foundational types: platform detection,
│   │   │                             #   memory utilities, disposal helpers,
│   │   │                             #   reference counting
│   │   ├── Auralis.Core.Mathematics/ # Math primitives: Vector2D/3D/4D,
│   │   │                             #   Quaternion, Matrix, Plane, Point,
│   │   │                             #   Rectangle, Size2D(-F), MathUtilities
│   │   └── Auralis.Core.IO/          # Core I/O abstractions
│   ├── AuralisRuntime/
│   │   └── Auralis.Engine/           # Engine runtime: Entity / Component model
│   │                                 #   (Entity, ComponentBase, EntityComponent,
│   │                                 #    TransformComponent, Script)
│   └── Auralis.Tools/
│       └── AssetPipeline/
│           └── Auralis.Tools.AssetPipeline.Interchange/
│                                     # Asset interchange: FBX reading/writing
│                                       (FBXHeader, FBXNode, FBXDocument,
│                                        FBXReader, FBXWriter)
└── Aurlais.World/                    # World layer (work in progress)
```

## Projects

| Project | Description |
| --- | --- |
| `Auralis.Core` | Base building blocks: `Platform`/`PlatformType` detection, `MemoryUtilities`, `DisposeBase`, `IReferenceCountable`. |
| `Auralis.Core.Mathematics` | Fully XML-documented math library with vectors, quaternions, matrices, planes, and geometric types. |
| `Auralis.Core.IO` | Input/output support for the core layer. |
| `Auralis.Engine` | Entity–component runtime with transforms and scripting hooks. |
| `Auralis.Tools.AssetPipeline.Interchange` | Interchange-format parsers/writers, currently focused on FBX documents. |
| `Aurlais.World` | Placeholder world/scene assembly layer (early stub). |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/) or newer
- An IDE that supports the `.slnx` solution format (e.g., recent versions of Visual Studio / Rider), or the `dotnet` CLI

## Getting Started

Clone the repository and restore/build the projects:

```bash
git clone https://github.com/mahdikshk/Auralis.git
cd Auralis

# Build everything
dotnet build Auralis.slnx

# Or build a single project
dotnet build src/Auralis.Core/Auralis.Core.Mathematics/Auralis.Core.Mathematics.csproj
```

## Status

Auralis is under heavy development. Many subsystems declared in the solution (graphics, rendering, physics, audio, animation, editor, platform) are placeholders awaiting implementation, and existing APIs may change without notice.

## License

License information has not been added to this repository yet. All rights reserved by the authors unless otherwise stated.