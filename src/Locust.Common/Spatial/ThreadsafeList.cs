// <fileheader>

using System;
using System.Collections.Generic;

namespace Locust;

// KoreThreadsafeList: Class to store new values in a thread-safe manner, allowing traversal through the list and addition/deletion of elements.

public class KoreThreadsafeList<T>
{
    private readonly List<T> _list = new List<T>();
    private readonly object _lock  = new object();

    public void Add(T item)
    {
        lock (_lock)
        {
            _list.Add(item);
        }
    }

    public void Remove(T item)
    {
        lock (_lock)
        {
            _list.Remove(item);
        }
    }

    public IReadOnlyList<T> GetSnapshot()
    {
        lock (_lock)
        {
            return _list.AsReadOnly();
        }
    }
}

