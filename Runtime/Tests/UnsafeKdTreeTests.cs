using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Meshia.MeshSimplification.Tests
{
    public class UnsafeKdTreeTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(7)]
        [TestCase(32)]
        [TestCase(99)]
        public void CoincidentPointsAreAllReturnedExactlyOnce(int count)
        {
            var points = new float3[count];
            CheckQuery(points, float3.zero, 0.0001f);
        }

        [Test]
        public void SphereQueriesMatchBruteForce()
        {
            var random = new Unity.Mathematics.Random(12345);
            var points = new float3[127];
            for (int i = 0; i < points.Length; i++)
                points[i] = random.NextFloat3(-1f, 1f);
            foreach (var center in points)
                CheckQuery(points, center, 0.6f);
            CheckQuery(points, new float3(10f), 0.1f);
        }

        static void CheckQuery(float3[] points, float3 center, float radius)
        {
            var tree = new UnsafeKdTree(Allocator.Temp);
            var results = new UnsafeList<int>(points.Length, Allocator.Temp);
            try
            {
                tree.Initialize(points);
                tree.QueryPointsInSphere(points, center, radius, ref results);
                var expected = new List<int>();
                var actual = new List<int>();
                for (int i = 0; i < points.Length; i++)
                    if (math.distancesq(points[i], center) < radius * radius)
                        expected.Add(i);
                for (int i = 0; i < results.Length; i++)
                    actual.Add(results[i]);
                CollectionAssert.AreEquivalent(expected, actual);
            }
            finally
            {
                results.Dispose();
                tree.Dispose();
            }
        }
    }
}
