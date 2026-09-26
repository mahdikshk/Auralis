# Auralis

Auralis is an open-source, cross-platform game engine and runtime framework written in **C#** targeting **.NET 10**. It provides a modular foundation for building interactive applications — spanning core mathematics, an entity-component engine runtime, an asset pipeline with FBX interchange support, and platform abstraction utilities.

## Repository Structure

The solution (`Auralis.slnx`) is organized into the following areas:

```
Auralis/
├── Auralis.slnx                          # .NET Solution (new XML format)
├── src/
│   ├── Auralis.Core/
│   │   ├── Auralis.Core                  # Core primitives: Platform detection, disposal helpers, memory utilities
│   │   ├── Auralis.Core.Mathematics      # Math types: Vector2D/3D/4D, Matrix, Quaternion, Plane, Point, Rectangle, Size2D, ...
│   │   └── Auralis.Core.IO               # Core I/O abstractions
│   ├── AuralisRuntime/
│   │   └── Auralis.Engine                # Runtime engine: Entity / Component model, TransformComponent, Scripting base types
│   └── Auralis.Tools/
│       └── AssetPipeline/
│           └── Auralis.Tools.AssetPipeline.Interchange   # Asset interchange formats (FBX reader/writer, document model)
├── Aurlais.World/                        # World module (early stage)
└── .idea/                                # JetBrains Rider project settings
```

Additional solution folders are reserved for planned modules: `Auralis.Animation`, `Auralis.Asset`, `Auralis.Audio`, `Auralis.Editor`, `Auralis.Graphics`, `Auralis.Memory`, `Auralis.Physics`, `Auralis.Platform`, `Auralis.Render`, `Auralis.Scene`, and `Auralis.Simulation`.

## Key Components

### Auralis.Core
Foundational library providing:
- **Platform detection** (`Platform` / `PlatformType`) for Windows, Linux, macOS, iOS, and Android.
- Memory utilities and reference-counting interfaces (`MemoryUtilities`, `IReferenceCountable`).
- Disposable base types (`DisposeBase`) and shared throw helpers.

### Auralis.Core.Mathematics
A high-performance math library built on blittable, sequentially-laid-out structs interoperating with `System.Numerics`:
- `Vector2D`, `Vector3D`, `Vector4D`
- `Matrix`, `Quaternion`, `Plane`
- `Point`, `Rectangle`, `Size2D`, `Size2DF`
- `MathUtilities` helpers

### Auralis.Engine
The runtime engine layer implementing an **Entity–Component** architecture:
- `Entity` with GUID identity and component dictionary
- `ComponentBase` / `EntityComponent` lifecycle hooks (`OnDestroy`, etc.)
- `TransformComponent` for spatial data
- `Script` base type for user gameplay code

### Auralis.Tools.AssetPipeline.Interchange
Asset import/export tooling with native **FBX** support:
- `FBXReader` / `FBXWriter` for binary FBX streams
- `FBXDocument`, `FBXNode`, `FBXHeader` document object model

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer
- A C# IDE is recommended: [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio 2022+](https://visualstudio.microsoft.com/)

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/mahdikshk/Auralis.git
   cd Auralis
   ```

2. **Restore and build the solution**

   ```bash
   dotnet restore Auralis.slnx
   dotnet build Auralis.slnx -c Release
   ```

3. **Run tests** *(test projects to be added under the `Tests/` solution folder)*

   ```bash
   dotnet test Auralis.slnx
   ```

## Usage Example

```csharp
using Auralis.Core.Mathematics;
using Auralis.Engine;

// Create an entity with a transform
var player = new Entity();
player.Transform.Position = new Vector3D(0, 1, 0);

// Use the math library
var direction = Vector3D.Normalize(new Vector3D(1, 0, 1));
```

## Status

Auralis is in **early / active development**. Many modules (graphics, audio, physics, editor) are scaffolded as solution folders but not yet implemented, and existing APIs may change without notice.

## License

See the repository for license information. If no license file is present, all rights are reserved by the copyright holders.
