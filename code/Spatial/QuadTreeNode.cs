namespace Locust.Spatial;

public sealed class QuadTreeNode
{
    private readonly object _sync = new();
    private QuadTreeNode[]? _children;
    private KoreMovingDouble _nodePing = KoreMovingDouble.Zero;

    public LLRect Bounds { get; }
    public QuadTreeNode? Parent { get; }
    public bool IsRoot => Parent is null;

    public KoreMovingDouble NodePing
    {
        get
        {
            lock (_sync)
            {
                return _nodePing;
            }
        }
        set
        {
            lock (_sync)
            {
                _nodePing = value;
            }
        }
    }

    public IReadOnlyList<QuadTreeNode> Children
    {
        get
        {
            lock (_sync)
            {
                return _children?.ToArray() ?? Array.Empty<QuadTreeNode>();
            }
        }
    }

    public bool HasChildren
    {
        get
        {
            lock (_sync)
            {
                return _children is not null;
            }
        }
    }


    internal QuadTreeNode(LLRect bounds, QuadTreeNode? parent = null)
    {
        Bounds = bounds;
        Parent = parent;
    }

    internal QuadTreeNode EnsureChildContaining(LLPoint point)
    {
        lock (_sync)
        {
            _children ??=
            [
                new QuadTreeNode(Bounds.TopLeft(), this),
                new QuadTreeNode(Bounds.TopRight(), this),
                new QuadTreeNode(Bounds.BottomLeft(), this),
                new QuadTreeNode(Bounds.BottomRight(), this)
            ];

            foreach (var child in _children)
            {
                if (child.Bounds.Contains(point))
                {
                    return child;
                }
            }

            throw new InvalidOperationException("Point was not contained by any child node.");
        }
    }

    internal QuadTreeNode? GetChildContaining(LLPoint point)
    {
        lock (_sync)
        {
            if (_children is null)
            {
                return null;
            }

            foreach (var child in _children)
            {
                if (child.Bounds.Contains(point))
                {
                    return child;
                }
            }

            return null;
        }
    }


    internal void ClearChildren()
    {
        lock (_sync)
        {
            _children = null;
        }
    }

}
