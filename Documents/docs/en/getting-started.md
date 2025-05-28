# Getting Started

## Installation

### VPM

Add [my VPM repository](https://ramtype0.github.io/VpmRepository/) to VCC, then add Meshia Mesh Simplification package to your projects.

## How to use

### NDMF integration

Attach `MeshiaMeshSimplifier` to your models.

You can preview the result in EditMode.

### Call from C#

```csharp

using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// Asynchronous API

await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// Synchronous API

MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);

```