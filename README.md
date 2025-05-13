# Meshia Mesh Simplification

- [English](#english)
- [日本語](#日本語)

## English
Mesh simplification tool/library for Unity.

Based on Unity Job System, and Burst. 
Provides fast, asynchronous mesh simplification.

Can be executed at runtime or in the editor.

### How to use

#### NDMF integration

Attach `NdmfMeshSimplifier` to your models.

You can preview the result in EditMode.


#### Call from C#

```C#

using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// Asynchronous API

await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// Synchronous API

MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);

```

## 日本語

Unity向けのメッシュ軽量化ツールです。
Unity Job Systemで動作するため、Burstと合わせて高速、かつ非同期で処理ができるのが特徴です。
ランタイム、エディターの双方で動作します。



### 使い方

#### NDMF統合

NDMFがプロジェクトにインポートされている場合、`NdmfMeshSimplifier`が使えます。
エディターで軽量化結果をプレビューしながらパラメーターの調整ができます。

#### C#から呼び出す

```C#

using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// 非同期API

await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// 同期API

MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);

```


