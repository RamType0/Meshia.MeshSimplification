using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Meshia.MeshSimplification
{
    [NativeContainer]
    unsafe struct NativeKeyedMinPriorityQueue
        <TKey, TValue> : INativeDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IComparable<TValue>
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeKeyedMinPriorityQueue<TKey, TValue>>();
#endif

        [NativeDisableUnsafePtrRestriction]
        UnsafeKeyedMinPriorityQueue<TKey, TValue>* priorityQueueData;
        public NativeKeyedMinPriorityQueue(AllocatorManager.AllocatorHandle allocator): this(1, allocator)
        {
            
        }
        
        public NativeKeyedMinPriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocator(allocator.Handle);
            CheckInitialCapacity(initialCapacity);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);

            CollectionHelper.SetStaticSafetyId<NativeKeyedMinPriorityQueue<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);

            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
            priorityQueueData = UnsafeKeyedMinPriorityQueue<TKey, TValue>.Create(initialCapacity, allocator);
        }
        public readonly UnsafeKeyedMinPriorityQueue<TKey, TValue>* GetUnsafeKeyedMinPriorityQueue() => priorityQueueData;
        public readonly int Count
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return priorityQueueData->Count;
            }
        }
        public readonly TValue this[TKey key]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return (*priorityQueueData)[key];
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                (*priorityQueueData)[key] = value;
            }
        }

        public readonly KeyValuePair<TKey, TValue> Peek()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return priorityQueueData->Peek();
        }
        public readonly KeyValuePair<TKey, TValue> Dequeue()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            return priorityQueueData->Dequeue();
        }
        public readonly bool TryDequeue([MaybeNullWhen(false)] out KeyValuePair<TKey, TValue> element)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            return priorityQueueData->TryDequeue(out element);
        }
        public readonly bool TryPeek([MaybeNullWhen(false)] out KeyValuePair<TKey, TValue> element)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return priorityQueueData->TryPeek(out element);
        }

        public readonly void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            priorityQueueData->Clear();
        }

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => priorityQueueData is not null;
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

            var jobHandle = new NativeKeyedMinPriorityQueueDisposeJob
            {
                Data = new()
                {
                    priorityQueueData = priorityQueueData,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_Safety = m_Safety,
#endif
                },
            }.Schedule(inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            priorityQueueData = null;

            return jobHandle;
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
            UnsafeKeyedMinPriorityQueue<TKey, TValue>.Destroy(priorityQueueData);
            priorityQueueData = null;
        }

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        unsafe struct NativeKeyedMinPriorityQueueDispose
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeKeyedMinPriorityQueue<TKey, TValue>* priorityQueueData;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif
            public readonly void Dispose()
            {
                UnsafeKeyedMinPriorityQueue<TKey, TValue>.Destroy(priorityQueueData);
            }
        }
        [BurstCompile]
        [GenerateTestsForBurstCompatibility]
        unsafe struct NativeKeyedMinPriorityQueueDisposeJob : IJob
        {
            internal NativeKeyedMinPriorityQueueDispose Data;

            public readonly void Execute()
            {
                Data.Dispose();
            }
        }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal static void CheckAllocator(AllocatorManager.AllocatorHandle allocator)
        {
            if (allocator.ToAllocator <= Allocator.None)
                throw new ArgumentException($"Allocator {allocator} must not be None or Invalid");
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }
    }

}


