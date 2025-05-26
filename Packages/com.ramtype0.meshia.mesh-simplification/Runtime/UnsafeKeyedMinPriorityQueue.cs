using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Meshia.MeshSimplification
{
    unsafe struct UnsafeKeyedMinPriorityQueue<TKey, TValue> : INativeDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IComparable<TValue>
    {
        internal UnsafeHashMap<TKey, int> keyToIndex;
        internal UnsafeList<KeyValuePair<TKey, TValue>> nodes;
        const int Arity = 4;
        const int Log2Arity = 2;
        public readonly int Count => nodes.Length;

        public UnsafeKeyedMinPriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            keyToIndex = new(initialCapacity, allocator);
            nodes = new(initialCapacity, allocator);
        }
        public static UnsafeKeyedMinPriorityQueue<TKey, TValue>* Create(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            var queue = AllocatorManager.Allocate<UnsafeKeyedMinPriorityQueue<TKey, TValue>>(allocator);
            *queue = new(initialCapacity, allocator);
            return queue;
        }
        public static void Destroy(UnsafeKeyedMinPriorityQueue<TKey, TValue>* queue)
        {
            CheckNull(queue);
            var allocator = queue->nodes.Allocator;
            queue->Dispose();
            AllocatorManager.Free(allocator, queue);
        }

        public TValue this[TKey key]
        {
            get
            {
                return nodes[keyToIndex[key]].Value;
            }
            set
            {
                var element = KeyValuePair.Create(key, value);
                if (keyToIndex.TryGetValue(element.Key, out int index))
                {
                    var oldElement = nodes[index];

                    switch (element.Value.CompareTo(oldElement.Value))
                    {
                        case < 0:
                            // The new value is less than the old one, so we need to move it up.
                            MoveUp(element, index);
                            return;
                        case > 0:
                            // The new value is greater than the old one, so we need to move it down.
                            MoveDown(element, index);
                            break;
                        default:
                            // The values are equal, no need to move.
                            nodes[index] = element;
                            break;
                    }
                    return;
                }
                else
                {
                    var currentSize = nodes.Length;
                    nodes.Length = currentSize + 1;
                    MoveUp(element, currentSize);
                }
            }
        }
        /// <summary>
        ///  Returns the minimal element from the <see cref="UnsafeMinPriorityQueue{T}"/> without removing it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The <see cref="UnsafeMinPriorityQueue{T}"/> is empty.</exception>
        /// <returns>The minimal element of the <see cref="UnsafeMinPriorityQueue{T}"/>.</returns>
        public KeyValuePair<TKey, TValue> Peek()
        {
            return nodes[0];
        }
        /// <summary>
        ///  Removes and returns the minimal element from the <see cref="UnsafeMinPriorityQueue{T}"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The queue is empty.</exception>
        /// <returns>The minimal element of the <see cref="UnsafeMinPriorityQueue{T}"/>.</returns>
        public KeyValuePair<TKey, TValue> Dequeue()
        {
            var element = nodes[0];
            RemoveRootNode();
            return element;
        }
        /// <summary>
        ///  Removes the minimal element from the <see cref="UnsafeMinPriorityQueue{T}"/>,
        ///  and copies it to the <paramref name="element"/> parameter.
        /// </summary>
        /// <param name="element">The removed element.</param>
        /// <returns>
        ///  <see langword="true"/> if the element is successfully removed;
        ///  <see langword="false"/> if the <see cref="UnsafeMinPriorityQueue{T}"/> is empty.
        /// </returns>
        public bool TryDequeue([MaybeNullWhen(false)] out KeyValuePair<TKey, TValue> element)
        {
            if (!nodes.IsEmpty)
            {
                element = nodes[0];
                RemoveRootNode();
                return true;
            }

            element = default;
            return false;
        }
        /// <summary>
        ///  Returns a value that indicates whether there is a minimal element in the <see cref="UnsafeMinPriorityQueue{T}"/>,
        ///  and if one is present, copies it to the <paramref name="element"/> parameter.
        ///  The element is not removed from the <see cref="UnsafeMinPriorityQueue{T}"/>.
        /// </summary>
        /// <param name="element">The minimal element in the queue.</param>
        /// <returns>
        ///  <see langword="true"/> if there is a minimal element;
        ///  <see langword="false"/> if the <see cref="UnsafeMinPriorityQueue{T}"/> is empty.
        /// </returns>
        public bool TryPeek([MaybeNullWhen(false)] out KeyValuePair<TKey, TValue> element)
        {
            if (!nodes.IsEmpty)
            {
                element = nodes[0];
                return true;
            }

            element = default;
            return false;
        }

        public void Clear()
        {
            keyToIndex.Clear();
            nodes.Clear();
        }

        /// <summary>
        /// Removes the node from the root of the heap
        /// </summary>
        private void RemoveRootNode()
        {
            var root = nodes[0];
            keyToIndex.Remove(root.Key);
            int lastNodeIndex = nodes.Length - 1;

            if (lastNodeIndex > 0)
            {
                var lastNode = nodes[lastNodeIndex];
                MoveDown(lastNode, 0);
            }
            nodes.Length -= 1;
        }
        /// <summary>
        /// Gets the index of an element's parent.
        /// </summary>
        [return: AssumeRange(0, (int.MaxValue - 1) >> Log2Arity)]
        private static int GetParentIndex(int index) => (index - 1) >> Log2Arity;

        /// <summary>
        /// Gets the index of the first child of an element.
        /// </summary>
        [return: AssumeRange(1, int.MaxValue)]
        private static int GetFirstChildIndex(int index) => (index << Log2Arity) + 1;
        /// <summary>
        /// Converts an unordered list into a heap.
        /// </summary>
        internal void Heapify()
        {
            // Leaves of the tree are in fact 1-element heaps, for which there
            // is no need to correct them. The heap property needs to be restored
            // only for higher nodes, starting from the first node that has children.
            // It is the parent of the very last element in the array.

            int lastParentWithChildren = GetParentIndex(nodes.Length - 1);
            if (keyToIndex.Capacity < nodes.Length)
            {
                keyToIndex.Capacity = nodes.Length;
            }
            for (int index = lastParentWithChildren; index >= 0; --index)
            {
                MoveDown(nodes[index], index);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetNode(KeyValuePair<TKey, TValue> node, int nodeIndex)
        {
            nodes[nodeIndex] = node;
            keyToIndex[node.Key] = nodeIndex;
        }
        /// <summary>
        /// Moves a node up in the tree to restore heap order.
        /// </summary>
        void MoveUp(KeyValuePair<TKey, TValue> node, int nodeIndex)
        {
            while (nodeIndex > 0)
            {
                var parentIndex = GetParentIndex(nodeIndex);
                var parent = nodes[parentIndex];
                if (node.Value.CompareTo(parent.Value) < 0)
                {
                    SetNode(parent, nodeIndex);
                    nodeIndex = parentIndex;
                }
                else
                {
                    break;
                }
            }
            SetNode(node, nodeIndex);
        }
        

        /// <summary>
        /// Moves a node down in the tree to restore heap order.
        /// </summary>
        void MoveDown(KeyValuePair<TKey, TValue> node, int nodeIndex)
        {
            int i;
            while ((i = GetFirstChildIndex(nodeIndex)) < nodes.Length)
            {
                // Find the child node with the minimal priority
                var minChild = nodes[i];
                var minChildIndex = i;
                var childIndexUpperBound = math.min(i + Arity, nodes.Length);
                while (++i < childIndexUpperBound)
                {
                    var nextChild = nodes[i];
                    if (nextChild.Value.CompareTo(minChild.Value) < 0)
                    {
                        minChild = nextChild;
                        minChildIndex = i;
                    }
                }
                // Heap property is satisfied; insert node in this location.
                if (node.Value.CompareTo(minChild.Value) <= 0)
                {
                    break;
                }
                // Move the minimal child up by one node and
                // continue recursively from its location.
                SetNode(minChild, nodeIndex);
                nodeIndex = minChildIndex;
            }
            SetNode(node, nodeIndex);
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            ;
            return JobHandle.CombineDependencies(keyToIndex.Dispose(inputDeps), nodes.Dispose(inputDeps));
        }

        public void Dispose()
        {
            keyToIndex.Dispose();
            nodes.Dispose();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal static void CheckNull(void* queue)
        {
            if (queue == null)
            {
                throw new InvalidOperationException("UnsafeMinPriorityQueue has yet to be created or has been destroyed!");
            }
        }
    }
}


