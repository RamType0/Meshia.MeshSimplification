# Meshia Mesh Simplification

Unity向けのメッシュ軽量化ツールです。
Unity Job Systemで動作するため、Burstと合わせて高速、かつ非同期で処理ができるのが特徴です。
ランタイム、エディターの双方で動作します。

## 使い方

MeshSimplifier.SimplifyAsyncを呼び出すだけです。Taskとして非同期で処理が実行されます。

## NDMF統合

NDMFがプロジェクトにインポートされている場合、`NdmfMeshSimplifier`が使えます。
エディターで軽量化結果をプレビューしながらパラメーターの調整ができます。
