using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meshia.MeshSimplification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meshia.MeshSimplification.Tests
{
    public class MeshSimplifierTests
    {
        // A Test behaves as an ordinary method
        [TestCase(PrimitiveType.Sphere)]
        [TestCase(PrimitiveType.Capsule)]
        [TestCase(PrimitiveType.Cylinder)]
        public async Task ShouldSimplifyPrimitive(PrimitiveType type)
        {
            var gameObject = GameObject.CreatePrimitive(type);
            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(gameObject);

            MeshSimplificationTarget target = new()
            {
                Kind = MeshSimplificationTargetKind.RelativeVertexCount,
                Value = 0.5f,
            };
            Mesh simplifiedMesh = new();
            await MeshSimplifier.SimplifyAsync(mesh, target, MeshSimplifierOptions.Default, simplifiedMesh);
            Object.Destroy(simplifiedMesh);
        }
    }

}
