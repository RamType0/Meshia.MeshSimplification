using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Meshia.MeshSimplification
{
    struct UnsafeMinHeap<T> : INativeDisposable
        where T : unmanaged, IComparable<T>
    {
        UnsafeList<T> buffer;
        public UnsafeMinHeap(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            buffer = new(initialCapacity, allocator);
        }
        /// <summary>
        /// Convert existing <see cref="NativeList{T}"/> to <see cref="UnsafeMinHeap{T}"/>. modifying <paramref name="list"/> after call this method will be resulted in unexpected behaviour.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public unsafe static UnsafeMinHeap<T>* ConvertExistingListToMinHeap(UnsafeList<T>* list)
        {
            var heap = (UnsafeMinHeap<T>*)list;
            heap->HeapifyInternalBuffer();
            return heap;
        }
        public static ref UnsafeMinHeap<T> ConvertExistingListToMinHeap(ref UnsafeList<T> list)
        {
            ref var heap = ref UnsafeUtility.As<UnsafeList<T>, UnsafeMinHeap<T>>(ref list);
            heap.HeapifyInternalBuffer();
            return ref heap;
        }
        void HeapifyInternalBuffer()
        {
            var parentCount = buffer.Length >> 1;
            if (parentCount > 0)
            {
                var selfIndex = parentCount - 1;
                if ((buffer.Length & 1) == 0)
                {
                    // Last parent has only one child
                    var childIndex = (selfIndex << 1) + 1;
                    ref var child = ref buffer.ElementAt(childIndex);

                    ref var self = ref buffer.ElementAt(selfIndex);

                    if (self.CompareTo(child) > 0)
                    {
                        (self, child) = (child, self);
                    }

                    selfIndex--;
                }
                while (selfIndex >= 0)
                {
                    var firstChildIndex = (selfIndex << 1) + 1;
                    ref var firstChild = ref buffer.ElementAt(firstChildIndex);

                    var secondChildIndex = firstChildIndex + 1;
                    ref var secondChild = ref buffer.ElementAt(secondChildIndex);

                    ref var minChild = ref firstChild.CompareTo(secondChild) < 0 ? ref firstChild : ref secondChild;

                    ref var self = ref buffer.ElementAt(selfIndex);

                    if (self.CompareTo(minChild) > 0)
                    {
                        (self, minChild) = (minChild, self);
                    }

                    selfIndex--;
                }
            }
        }

        public void Push(in T value)
        {
            var selfIndex = buffer.Length;
            buffer.Add(value);
            while (selfIndex > 0)
            {
                var parentIndex = (selfIndex - 1) >> 1;
                ref var self = ref buffer.ElementAt(selfIndex);
                ref var parent = ref buffer.ElementAt(parentIndex);
                if (self.CompareTo(parent) < 0)
                {
                    (self, parent) = (parent, self);
                }
                else
                {
                    break;
                }
                selfIndex = parentIndex;
            }
        }
        public bool TryPop(out T value)
        {
            if (buffer.IsEmpty)
            {
                value = default;
                return false;
            }
            else
            {
                value = Pop();
                return true;
            }
        }
        public T Pop()
        {
            var popped = buffer[0];
            buffer.RemoveAtSwapBack(0);

            var selfIndex = 0;

            while (true)
            {
                var firstChildIndex = (selfIndex << 1) + 1;
                if (buffer.Length <= firstChildIndex)
                {
                    break;
                }

                ref var firstChild = ref buffer.ElementAt(firstChildIndex);

                ref var minChild = ref firstChild;
                var minChildIndex = firstChildIndex;

                var secondChildIndex = firstChildIndex + 1;
                if (secondChildIndex < buffer.Length)
                {
                    ref var secondChild = ref buffer.ElementAt(secondChildIndex);
                    if (firstChild.CompareTo(secondChild) > 0)
                    {
                        minChild = ref secondChild;
                        minChildIndex = secondChildIndex;
                    }
                }

                ref var self = ref buffer.ElementAt(selfIndex);

                if (self.CompareTo(minChild) > 0)
                {
                    (self, minChild) = (minChild, self);
                }
                else
                {
                    break;
                }
                selfIndex = minChildIndex;
            }
            return popped;
        }
        public ref readonly T Peek()
        {
            return ref buffer.ElementAt(0);
        }
        public bool TryPeek(out T value)
        {
            if (buffer.IsEmpty)
            {
                value = default;
                return false;
            }
            else
            {
                value = Peek();
                return true;
            }
        }
        public JobHandle Dispose(JobHandle inputDeps) => buffer.Dispose(inputDeps);

        public void Dispose() => buffer.Dispose();
    }

}


