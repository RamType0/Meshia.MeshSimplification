# C#から呼び出す

Meshiaの中核的なAPIは全て[`MeshSimplifier`](~/api/Meshia.MeshSimplification.MeshSimplifier.html)を通じて提供されます。

`MeshSimplifier`には`static`メソッドで提供されるステートレスAPIと、より高度なシナリオ向けのステートフルAPIがあります。

## ステートレスAPI

```csharp

using Meshia.MeshSimplification;

Mesh simplifiedMesh = new();

// 非同期API

await MeshSimplifier.SimplifyAsync(originalMesh, target, options, simplifiedMesh);

// 同期API

MeshSimplifier.Simplify(originalMesh, target, options, simplifiedMesh);

```

## ステートフルAPI

複数フレームをまたいで非同期で処理を行う場合、`Allocator.Persistent`を使用してください。
1フレーム以内に処理が終わる場合でも、`Allocator.TempJob`を用いる必要があります。(`Allocator.Temp`は使えません)

1. `MeshSimplifier`のインスタンスを作る

2. [`MeshSimplifier.ScheduleLoadMeshData`](~/api/Meshia.MeshSimplification.MeshSimplifier.html#Meshia_MeshSimplification_MeshSimplifier_ScheduleLoadMeshData_Mesh_MeshData_Meshia_MeshSimplification_MeshSimplifierOptions_JobHandle_)でメッシュの読み込みをスケジュールする

3. [`BlendShapeData.GetMeshBlendShapes`](~/api/Meshia.MeshSimplification.BlendShapeData.html#Meshia_MeshSimplification_BlendShapeData_GetMeshBlendShapes_Mesh_AllocatorManager_AllocatorHandle_)でメッシュのブレンドシェイプを読み込む

4. [`MeshSimplifier.ScheduleSimplify`](~/api/Meshia.MeshSimplification.MeshSimplifier.html#Meshia_MeshSimplification_MeshSimplifier_ScheduleSimplify_Mesh_MeshData_NativeList_Meshia_MeshSimplification_BlendShapeData__Meshia_MeshSimplification_MeshSimplificationTarget_JobHandle_)
でメッシュの軽量化をスケジュールする

5. [`MeshSimplifier.ScheduleWriteMeshData`](~/api/Meshia.MeshSimplification.MeshSimplifier.html#Meshia_MeshSimplification_MeshSimplifier_ScheduleWriteMeshData_Mesh_MeshData_NativeList_Meshia_MeshSimplification_BlendShapeData__Mesh_MeshData_NativeList_Meshia_MeshSimplification_BlendShapeData__JobHandle_)でメッシュの情報を書き出す

4~5を繰り返すことで一つのメッシュの複数のLODを生成することができます。


[`MeshSimplifier.SimplifyAsync`](~/api/Meshia.MeshSimplification.MeshSimplifier.html#Meshia_MeshSimplification_MeshSimplifier_SimplifyAsync_Mesh_Meshia_MeshSimplification_MeshSimplificationTarget_Meshia_MeshSimplification_MeshSimplifierOptions_Mesh_System_Threading_CancellationToken_)の実装や、[テストコード](https://github.com/RamType0/Meshia.MeshSimplification/blob/d2ab9e170db6f7c6bbf693dd205415178a06c857/Runtime/Tests/MeshSimplifierTests.cs#L42)も参考になるでしょう。
