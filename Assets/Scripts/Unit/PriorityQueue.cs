using System;
using System.Collections.Generic;

public class PriorityQueue<T> where T : IComparable<T>
{
    private readonly List<T> data;

    public PriorityQueue(int capacity = 100)
    {
        data = new List<T>(capacity);
    }

    public int Count {
        get {
            return data.Count;
        }
    }

    public void Enqueue(T item)
    {
        data.Add(item);
        int childIndex = data.Count - 1;

        while (childIndex > 0) {
            int parentIndex = (childIndex - 1) / 2;

            if (data[parentIndex].CompareTo(item) <= 0) break;

            T tmp = data[childIndex];
            data[childIndex] = data[parentIndex];
            data[parentIndex] = tmp;

            childIndex = parentIndex;
        }
    }

    public T Dequeue()
    {
        int lastIndex = data.Count - 1;
        T frontItem = data[0];
        data[0] = data[lastIndex];
        data.RemoveAt(lastIndex);

        lastIndex--;

        int parentIndex = 0;
        while (true) {
            int childIndex = parentIndex * 2 + 1;
            if (childIndex > lastIndex) break;

            int rightChild = childIndex + 1;
            if (rightChild <= lastIndex && data[rightChild].CompareTo(data[childIndex]) < 0) {
                childIndex = rightChild;
            }

            if (data[parentIndex].CompareTo(data[childIndex]) <= 0) break;

            T tmp = data[parentIndex];
            data[parentIndex] = data[childIndex];
            data[childIndex] = tmp;

            parentIndex = childIndex;
        }

        return frontItem;
    }

    public void Clear()
    {
        data.Clear();
    }

    public bool Contains(T item)
    {
        return data.Contains(item);
    }
}
