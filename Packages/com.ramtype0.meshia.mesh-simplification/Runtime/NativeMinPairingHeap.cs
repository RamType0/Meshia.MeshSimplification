using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Meshia.MeshSimplification
{
    [NativeContainer]
    unsafe struct NativeMinPairingHeap<T> : INativeDisposable
        where T : unmanaged, IComparable<T>
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeList<T>>();
#endif
        [NativeDisableUnsafePtrRestriction]
        UnsafeMinPairingHeap<T>* heapData;
        public NativeMinPairingHeap(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator.ToAllocator <= Allocator.None)
                throw new ArgumentException($"Allocator {allocator} must not be None or Invalid");

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);

            CollectionHelper.SetStaticSafetyId<NativeList<T>>(ref m_Safety, ref s_staticSafetyId.Data);

            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            heapData = UnsafeMinPairingHeap<T>.Create(allocator);
        }
        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => heapData is not null;
        }
        public readonly void Enqueue(in T value)
        {

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            heapData->Enqueue(in value);
        }
        public readonly bool TryDequeue(out T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            return heapData->TryDequeue(out value);
        }
        public readonly T Dequeue()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            return heapData->Dequeue();
        }
        public readonly bool TryPeek(out T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return heapData->TryPeek(out value);
        }
        public readonly void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            heapData->Clear();
        }
        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => heapData->IsEmpty;
        }
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
            {
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            }
#endif
            if (!IsCreated)
            {
                return;
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            UnsafeMinPairingHeap<T>.Destroy(heapData);
            heapData = null;
        }
        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
            {
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            }
#endif
            if (!IsCreated)
            {
                return inputDeps;
            }


            var jobHandle = new DisposeJob 
            { 
                Data = new()
                { 
                    HeapData = heapData,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_Safety = m_Safety
#endif
                },
            }.Schedule(inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            heapData = null;
            return jobHandle;
        }

        [NativeContainer]
        unsafe struct NativeMinPairingHeapDispose
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeMinPairingHeap<T>* HeapData;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif
            public void Dispose()
            {
                UnsafeMinPairingHeap<T>.Destroy(HeapData);
            }
        }

        [BurstCompile]
        struct DisposeJob : IJob
        {
            internal NativeMinPairingHeapDispose Data;
            public void Execute()
            {
                Data.Dispose();
            }
        }
    }

    unsafe struct UnsafeMinPairingHeap<T> : IDisposable
        where T : unmanaged, IComparable<T>
    {
        internal struct Node
        {
            public T Value;
            public Node* FirstChild, NextSibling;

        }
        public UnsafeMinPairingHeap(AllocatorManager.AllocatorHandle allocator)
        {
            Allocator = allocator;
            Root = null;
            Count = 0;
        }

        public static UnsafeMinPairingHeap<T>* Create(AllocatorManager.AllocatorHandle allocator)
        {
            var heap = AllocatorManager.Allocate<UnsafeMinPairingHeap<T>>(allocator);

            *heap = new(allocator);
            return heap;
        }


        public static void Destroy(UnsafeMinPairingHeap<T>* heap)
        {
            if(heap is null)
            {
                throw new ArgumentNullException(nameof(heap), "Heap cannot be null.");
            }
            var allocator = heap->Allocator;
            heap->Dispose();
            AllocatorManager.Free(allocator, heap);
        }

        [NativeDisableUnsafePtrRestriction]
        internal Node* Root;
        internal AllocatorManager.AllocatorHandle Allocator;
        int Count;
        static Node* Link(Node* a, Node* b)
        {
            if (a is null) return b;
            if (b is null) return a;

            Node* parent, child;

            if(a->Value.CompareTo(b->Value) < 0)
            {
                parent = a;

                child = b;
            }
            else
            {
                parent = b;
                child = a;
            }

            child->NextSibling = parent->FirstChild;
            parent->FirstChild = child;
            return parent;
        }

        static Node* Extract(Node* s)
        {
            Node* next = null;
            while(s is not null)
            {
                Node* a = s;
                Node* b = null;
                s = s->NextSibling;
                a->NextSibling = null;
                if(s is not null)
                {
                    b = s;
                    s= s->NextSibling;
                    b->NextSibling = null;
                }
                a = Link(a, b);
                a->NextSibling = next;
                next = a;
            }
            while(next is not null)
            {

                Node* j = next;
                next = next->NextSibling;
                s = Link(j, s);
            }
            return s;

        }

        public void Enqueue(in T value)
        {
            var newNode = AllocatorManager.Allocate<Node>(Allocator);
            *newNode = new()
            {
                Value = value,
            };
            Root = Link(Root, newNode);

            Count++;
        }

        public bool TryDequeue([MaybeNullWhen(false)] out T value)
        {
            if (IsEmpty)
            {
                value = default;
                return false;
            }
            value = Dequeue();
            return true;
        }

        public T Dequeue()
        {
            CheckNotEmpty();
            T value = Root->Value;
            RemoveRootNode();
            return value;
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void CheckNotEmpty()
        {
            if (IsEmpty)
            {
                ThrowEmpty();
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ThrowEmpty()
        {
            throw new InvalidOperationException("Trying to read from an empty heap.");
        }

        private void RemoveRootNode()
        {
            Node* oldRoot = Root;
            Root = Extract(Root->FirstChild);
            AllocatorManager.Free(Allocator, oldRoot);
            Count--;
        }

        public readonly bool TryPeek([MaybeNullWhen(false)] out T value)
        {
            if (IsEmpty)
            {
                value = default;
                return false;
            }
            value = Root->Value;
            return true;
        }

        public readonly T Peek()
        {
            CheckNotEmpty();
            return Root->Value;
        }

        public readonly bool IsEmpty => Root is null;

        public void Clear()
        {
            while(TryDequeue(out _))
            {

            }

            return;

            if (IsEmpty)
            {
                return;
            }

            UnsafePtrList<Node> nodes = new(Count, AllocatorManager.Temp)
            {
                Root
            };
            while (!nodes.IsEmpty)
            {
                var node = nodes[^1];
                nodes.Length -= 1;
                if (node->FirstChild is not null)
                {
                    nodes.AddNoResize(node->FirstChild);
                }
                if (node->NextSibling is not null)
                {
                    nodes.AddNoResize(node->NextSibling);
                }
                AllocatorManager.Free(Allocator, node);
            }
            nodes.Dispose();

            Root = null;
            Count = 0;
        }
        public void Dispose()
        {
            Clear();
            Allocator = AllocatorManager.Invalid;
        }

    }
}
