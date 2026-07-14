namespace Locust;

public readonly record struct QuadTreeRectCell<TValue>(
    QuadTreePosition RequestedPosition,
    QuadTreePosition ResolvedPosition,
    LLRect Bounds,
    TValue Value,
    bool HasExactNode,
    bool IsInsideTree);
