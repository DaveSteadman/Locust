namespace Locust.Spatial;

public sealed class QuadTree
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public QuadTree() : this(LLRect.EarthDegrees)
    {
    }

    public QuadTree(LLRect bounds)
    {
        if (!bounds.IsValid())
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Tree bounds must have positive width and height.");
        }

        Root = new QuadTreeNode(bounds);
    }

    public QuadTreeNode Root { get; }

    internal void EnterReadLock()
    {
        _lock.EnterReadLock();
    }

    internal void ExitReadLock()
    {
        _lock.ExitReadLock();
    }

    internal void EnterWriteLock()
    {
        _lock.EnterWriteLock();
    }

    internal void ExitWriteLock()
    {
        _lock.ExitWriteLock();
    }
}
