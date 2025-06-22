# チュートリアル

## インストール

### VPM

[VPM repository](https://ramtype0.github.io/VpmRepository/)をVCCに追加してから、Manage Project > Manage PackagesからMeshia Mesh Simplificationをプロジェクトに追加してください。

## 使い方

### NDMF統合

NDMFがプロジェクトにインポートされている場合、`MeshiaMeshSimplifier`が使えます。
エディターで軽量化結果をプレビューしながらパラメーターの調整ができます。

### C#から呼び出す

```csharp

using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// 非同期API

await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// 同期API

MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);

```


