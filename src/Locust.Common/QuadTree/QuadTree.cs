
namespace Locust;

public sealed class QuadTree<TPayload>
{
    // The capacity of the quad tree, which is the maximum number of nodes it can contain.
    public const int DefaultCapacity = 100_000;

    // Locking, done with a ReaderWriterLockSlim to allow multiple concurrent readers but only one writer at a time.
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    // The array of nodes in the quad tree. Each node represents a rectangular region of space and can contain a payload of type TPayload.
    private readonly QuadTreeNode<TPayload>[] _nodes;

    // A stack of free indices in the _nodes array. When a node is removed, its index is pushed onto this stack so it can be reused later.
    private readonly Stack<int> _freeIndices;

    // --------------------------------------------------------------------------------------------
    // MARK: Constructors
    // --------------------------------------------------------------------------------------------

    public QuadTree() : this(LLRect.EarthDegrees, DefaultCapacity)
    {
    }

    public QuadTree(int capacity) : this(LLRect.EarthDegrees, capacity)
    {
    }

    public QuadTree(LLRect bounds, int capacity = DefaultCapacity)
    {
        if (!bounds.IsValid())
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Tree bounds must have positive width and height.");
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Tree capacity must be at least 1.");
        }

        _nodes = new QuadTreeNode<TPayload>[capacity];
        for (var i = 0; i < capacity; i++)
        {
            _nodes[i] = new QuadTreeNode<TPayload>();
        }

        _freeIndices = new Stack<int>(Math.Max(capacity - 1, 0));
        for (var i = capacity - 1; i >= 1; i--)
        {
            _freeIndices.Push(i);
        }

        RootIndex = 0;
        _nodes[RootIndex].Initialise(bounds, QuadTreeNode<TPayload>.EmptyIndex);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Whole Tree Ops
    // --------------------------------------------------------------------------------------------

    public int RootIndex { get; }

    public QuadTreeNode<TPayload> Root => _nodes[RootIndex];

    public int Capacity => _nodes.Length;

    // --------------------------------------------------------------------------------------------
    // MARK: Node Management
    // --------------------------------------------------------------------------------------------

    public QuadTreeNode<TPayload> GetNode(int index)
    {
        if ((uint)index >= (uint)_nodes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _nodes[index];
    }

    internal int EnsureChildContaining(int parentIndex, LLPoint point)
    {
        var parent = GetNode(parentIndex);

        if (!parent.HasChildren)
        {
            parent.TopLeftIndex     = AllocateNode(parent.Bounds.TopLeft(), parentIndex);
            parent.TopRightIndex    = AllocateNode(parent.Bounds.TopRight(), parentIndex);
            parent.BottomLeftIndex  = AllocateNode(parent.Bounds.BottomLeft(), parentIndex);
            parent.BottomRightIndex = AllocateNode(parent.Bounds.BottomRight(), parentIndex);
        }
        if (_nodes[parent.TopLeftIndex].Bounds.Contains(point))     { return parent.TopLeftIndex; }
        if (_nodes[parent.TopRightIndex].Bounds.Contains(point))    { return parent.TopRightIndex; }
        if (_nodes[parent.BottomLeftIndex].Bounds.Contains(point))  { return parent.BottomLeftIndex; }
        if (_nodes[parent.BottomRightIndex].Bounds.Contains(point)) { return parent.BottomRightIndex; }

        throw new InvalidOperationException("Point was not contained by any child node.");
    }

    internal int EnsureChildIndex(int parentIndex, char quadrantDigit)
    {
        var parent = GetNode(parentIndex);

        if (!parent.HasChildren)
        {
            parent.TopLeftIndex     = AllocateNode(parent.Bounds.TopLeft(), parentIndex);
            parent.TopRightIndex    = AllocateNode(parent.Bounds.TopRight(), parentIndex);
            parent.BottomLeftIndex  = AllocateNode(parent.Bounds.BottomLeft(), parentIndex);
            parent.BottomRightIndex = AllocateNode(parent.Bounds.BottomRight(), parentIndex);
        }

        return GetChildIndex(parentIndex, quadrantDigit);
    }

    internal int GetChildContaining(int parentIndex, LLPoint point)
    {
        var parent = GetNode(parentIndex);
        if (!parent.HasChildren)
        {
            return QuadTreeNode<TPayload>.EmptyIndex;
        }

        if (_nodes[parent.TopLeftIndex].Bounds.Contains(point))     { return parent.TopLeftIndex; }
        if (_nodes[parent.TopRightIndex].Bounds.Contains(point))    { return parent.TopRightIndex; }
        if (_nodes[parent.BottomLeftIndex].Bounds.Contains(point))  { return parent.BottomLeftIndex; }
        if (_nodes[parent.BottomRightIndex].Bounds.Contains(point)) { return parent.BottomRightIndex; }

        return QuadTreeNode<TPayload>.EmptyIndex;
    }

    internal int GetChildIndex(int parentIndex, char quadrantDigit)
    {
        var parent = GetNode(parentIndex);

        return quadrantDigit switch
        {
            QuadTreePosition.TopLeftDigit     => parent.TopLeftIndex,
            QuadTreePosition.TopRightDigit    => parent.TopRightIndex,
            QuadTreePosition.BottomLeftDigit  => parent.BottomLeftIndex,
            QuadTreePosition.BottomRightDigit => parent.BottomRightIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(quadrantDigit), "Quadrant digit must be 1, 2, 3, or 4.")
        };
    }

    internal void ClearChildren(int parentIndex)
    {
        var parent = GetNode(parentIndex);
        if (!parent.HasChildren)
        {
            return;
        }

        ReleaseSubtree(parent.TopLeftIndex);
        ReleaseSubtree(parent.TopRightIndex);
        ReleaseSubtree(parent.BottomLeftIndex);
        ReleaseSubtree(parent.BottomRightIndex);

        parent.TopLeftIndex     = QuadTreeNode<TPayload>.EmptyIndex;
        parent.TopRightIndex    = QuadTreeNode<TPayload>.EmptyIndex;
        parent.BottomLeftIndex  = QuadTreeNode<TPayload>.EmptyIndex;
        parent.BottomRightIndex = QuadTreeNode<TPayload>.EmptyIndex;
    }

    private int AllocateNode(LLRect bounds, int parentIndex)
    {
        if (!_freeIndices.TryPop(out var index))
        {
            throw new InvalidOperationException($"QuadTree capacity of {_nodes.Length} nodes has been exhausted.");
        }

        _nodes[index].Initialise(bounds, parentIndex);
        return index;
    }

    private void ReleaseSubtree(int nodeIndex)
    {
        if (nodeIndex == QuadTreeNode<TPayload>.EmptyIndex)
        {
            return;
        }

        var node = _nodes[nodeIndex];
        if (node.HasChildren)
        {
            ReleaseSubtree(node.TopLeftIndex);
            ReleaseSubtree(node.TopRightIndex);
            ReleaseSubtree(node.BottomLeftIndex);
            ReleaseSubtree(node.BottomRightIndex);
        }

        node.ClearLinksAndPayload();
        _freeIndices.Push(nodeIndex);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Locking
    // --------------------------------------------------------------------------------------------

    internal void EnterReadLock()  { _lock.EnterReadLock(); }
    internal void ExitReadLock()   { _lock.ExitReadLock(); }
    internal void EnterWriteLock() { _lock.EnterWriteLock(); }
    internal void ExitWriteLock()  { _lock.ExitWriteLock(); }
}
