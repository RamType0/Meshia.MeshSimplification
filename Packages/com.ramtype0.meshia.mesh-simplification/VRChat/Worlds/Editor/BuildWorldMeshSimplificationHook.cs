using Meshia.MeshSimplification;
using Meshia.MeshSimplification.Ndmf;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor;
using VRC.SDK3.Editor;

public class BuildWorldMeshSimplificationHook
{
    [InitializeOnLoadMethod]
    public static void RegisterSdkCallback()
    {
        VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnable;
    }

    private static void OnSdkPanelEnable(object sender, EventArgs e)
    {
        if(VRCSdkControlPanel.TryGetBuilder<IVRCSdkWorldBuilderApi>(out var builder))
        {
            builder.OnSdkBuildStateChange += OnSdkBuildStateChange;
        }
    }
    static Dictionary<string, Mesh> SimplifiedMeshToOriginalMesh { get; } = new();
    private static void OnSdkBuildStateChange(object sender, SdkBuildState e)
    {
        switch (e)
        {
            case SdkBuildState.Building:
                {
                    try
                    {
                        BackupOriginalMeshesAndSetSimplifiedMeshes();
                    }
                    catch (Exception)
                    {
                        RestoreOriginalMeshes();
                        throw;
                    }
                }
                break;
            case SdkBuildState.Success:
            case SdkBuildState.Failure:
                {
                    RestoreOriginalMeshes();
                }
                break;
        }
    }

    private static void RestoreOriginalMeshes()
    {
        foreach (var ndmfMeshSimplifier in VRC.Tools.FindSceneObjectsOfTypeAll<NdmfMeshSimplifier>())
        {
            if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                meshFilter.sharedMesh = SimplifiedMeshToOriginalMesh[meshFilter.sharedMesh.name];
            }
            else if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer) && skinnedMeshRenderer.sharedMesh != null)
            {
                skinnedMeshRenderer.sharedMesh = SimplifiedMeshToOriginalMesh[skinnedMeshRenderer.sharedMesh.name];
            }
        }
        SimplifiedMeshToOriginalMesh.Clear();
    }

    private static void BackupOriginalMeshesAndSetSimplifiedMeshes()
    {
        foreach (var ndmfMeshSimplifier in VRC.Tools.FindSceneObjectsOfTypeAll<NdmfMeshSimplifier>())
        {
            if (ndmfMeshSimplifier.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                var originalMesh = meshFilter.sharedMesh;
                Mesh simplifiedMesh = new();


                MeshSimplifier.Simplify(originalMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);

                var key = Guid.NewGuid().ToString();

                simplifiedMesh.name = key;
                SimplifiedMeshToOriginalMesh.Add(key, originalMesh);
                meshFilter.sharedMesh = simplifiedMesh;
            }
            else if (ndmfMeshSimplifier.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer) && skinnedMeshRenderer.sharedMesh != null)
            {
                var originalMesh = skinnedMeshRenderer.sharedMesh;
                Mesh simplifiedMesh = new();


                MeshSimplifier.Simplify(originalMesh, ndmfMeshSimplifier.target, ndmfMeshSimplifier.options, simplifiedMesh);


                var key = Guid.NewGuid().ToString();

                simplifiedMesh.name = key;

                SimplifiedMeshToOriginalMesh.Add(key, originalMesh);
                skinnedMeshRenderer.sharedMesh = simplifiedMesh;
            }
        }
    }
}
