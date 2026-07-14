namespace Locust;

public sealed class QuadTreeNode<TPayload>
{
    // The value used to indicate that a node index is blank or unset.
    public const int EmptyIndex = -1;

    // The rectangular bounds covered by this node.
    public LLRect Bounds { get; internal set; }

    // The parent and child node indices. These all point into the owning quad tree's static node array.
    public int ParentIndex      { get; internal set; } = EmptyIndex;
    public int TopLeftIndex     { get; internal set; } = EmptyIndex;
    public int TopRightIndex    { get; internal set; } = EmptyIndex;
    public int BottomLeftIndex  { get; internal set; } = EmptyIndex;
    public int BottomRightIndex { get; internal set; } = EmptyIndex;

    // Payload state for this node.
    public bool HasPayload { get; internal set; }
    public TPayload Payload { get; internal set; } = default!;

    // --------------------------------------------------------------------------------------------
    // MARK: State
    // --------------------------------------------------------------------------------------------

    public bool IsRoot      => ParentIndex == EmptyIndex;
    public bool HasChildren => TopLeftIndex != EmptyIndex;

    // --------------------------------------------------------------------------------------------
    // MARK: Lifecycle
    // --------------------------------------------------------------------------------------------

    internal void Initialise(LLRect bounds, int parentIndex)
    {
        Bounds           = bounds;
        ParentIndex      = parentIndex;
        TopLeftIndex     = EmptyIndex;
        TopRightIndex    = EmptyIndex;
        BottomLeftIndex  = EmptyIndex;
        BottomRightIndex = EmptyIndex;
        HasPayload       = false;
        Payload          = default!;
    }

    internal void ClearLinksAndPayload()
    {
        Bounds           = default;
        ParentIndex      = EmptyIndex;
        TopLeftIndex     = EmptyIndex;
        TopRightIndex    = EmptyIndex;
        BottomLeftIndex  = EmptyIndex;
        BottomRightIndex = EmptyIndex;
        HasPayload       = false;
        Payload          = default!;
    }
}
