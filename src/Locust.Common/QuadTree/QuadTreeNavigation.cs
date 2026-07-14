

namespace Locust;

public static class QuadTreeNavigation
{
    // --------------------------------------------------------------------------------------------
    // MARK: Insert / Navigate
    // --------------------------------------------------------------------------------------------

    public static int NavigateToContainingNode(QuadTree<QuadTreePayload> tree, LLPing ping)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (ping.RadiusDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(ping.RadiusDegs), "Max span must be greater than zero.");
        }

        if (!tree.Root.Bounds.Contains(ping.Center))
        {
            throw new ArgumentOutOfRangeException(nameof(ping.Center), "Point must be inside the tree bounds.");
        }

        tree.EnterWriteLock();
        try
        {
            var nodeIndex = tree.RootIndex;
            while (tree.GetNode(nodeIndex).Bounds.MaxSpanDegrees > ping.RadiusDegs)
            {
                nodeIndex = tree.EnsureChildContaining(nodeIndex, ping.Center);
            }

            var node = tree.GetNode(nodeIndex);
            var payload = new QuadTreePayload();
            payload.SetValue(ping.Strength, ping.DecaySecs);
            node.Payload = payload;
            node.HasPayload = true;

            return nodeIndex;
        }
        finally
        {
            tree.ExitWriteLock();
        }
    }

    public static int SetPayloadValue(QuadTree<QuadTreePayload> tree, QuadTreePosition position, double value, double decaySecs)
    {
        ArgumentNullException.ThrowIfNull(tree);

        tree.EnterWriteLock();
        try
        {
            var nodeIndex = EnsureNodeAtPosition(tree, position);
            var node = tree.GetNode(nodeIndex);
            var payload = node.HasPayload ? node.Payload : default;
            payload.SetValue(value, decaySecs);
            node.Payload = payload;
            node.HasPayload = true;

            return nodeIndex;
        }
        finally
        {
            tree.ExitWriteLock();
        }
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Query
    // --------------------------------------------------------------------------------------------

    public static QuadTreeRectCell<TValue>[,] GetRect<TPayload, TValue>(QuadTree<TPayload> tree, QuadTreePosition centerPosition, int widthcount, int heightcount, Func<QuadTree<TPayload>, int, QuadTreeNode<TPayload>, TValue> valueSelector, TValue outsideValue = default!)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(valueSelector);

        if (widthcount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(widthcount), "Width count must be at least 1.");
        }

        if (heightcount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(heightcount), "Height count must be at least 1.");
        }

        centerPosition.GetGridCoordinates(out var centerColumn, out var centerRow);

        var depth = centerPosition.Depth;
        var dimension = 1 << depth;
        var leftOffset = widthcount / 2;
        var topOffset = heightcount / 2;
        var rect = new QuadTreeRectCell<TValue>[widthcount, heightcount];

        tree.EnterReadLock();
        try
        {
            for (var y = 0; y < heightcount; y++)
            {
                for (var x = 0; x < widthcount; x++)
                {
                    var column = centerColumn + x - leftOffset;
                    var row = centerRow + y - topOffset;

                    if (column < 0 || column >= dimension || row < 0 || row >= dimension)
                    {
                        rect[x, y] = new QuadTreeRectCell<TValue>(
                            centerPosition,
                            centerPosition,
                            LLRect.Zero,
                            outsideValue,
                            false,
                            false);

                        continue;
                    }

                    var requestedPosition = QuadTreePosition.FromGridCoordinates(depth, column, row);
                    var (resolvedIndex, resolvedPosition, hasExactNode) = NavigateToExistingNode(tree, requestedPosition);
                    var resolvedNode = tree.GetNode(resolvedIndex);

                    rect[x, y] = new QuadTreeRectCell<TValue>(
                        requestedPosition,
                        resolvedPosition,
                        requestedPosition.ToBounds(tree.Root.Bounds),
                        valueSelector(tree, resolvedIndex, resolvedNode),
                        hasExactNode,
                        true);
                }
            }

            return rect;
        }
        finally
        {
            tree.ExitReadLock();
        }
    }

    public static QuadTreeRectCell<double>[,] GetStrengthRect(QuadTree<QuadTreePayload> tree, QuadTreePosition centerPosition, int widthcount, int heightcount)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return GetRect(
            tree,
            centerPosition,
            widthcount,
            heightcount,
            static (currentTree, nodeIndex, _) => AccumulateStrengthFromNode(currentTree, nodeIndex),
            0d);
    }

    public static KoreDouble2DArray StrengthValuesToArray(QuadTree<QuadTreePayload> tree, LLRect bounds, int widthcount, int heightcount)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (widthcount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(widthcount), "Width count must be at least 1.");
        }

        if (heightcount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(heightcount), "Height count must be at least 1.");
        }

        KoreDouble2DArray strengthArray = new(widthcount, heightcount);

        var lonCellWidthDegs = bounds.WidthDegs / widthcount;
        var latCellHeightDegs = bounds.HeightDegs / heightcount;
        var cellSpanDegs = Math.Max(lonCellWidthDegs, latCellHeightDegs);

        for (int y = 0; y < heightcount; y++)
        {
            for (int x = 0; x < widthcount; x++)
            {
                var point = new LLPoint(
                    bounds.LLonDegs + ((x + 0.5d) * lonCellWidthDegs),
                    bounds.TLatDegs + ((y + 0.5d) * latCellHeightDegs));

                strengthArray[x, y] = GetPingStrengthAtPoint(tree, point, cellSpanDegs);
            }
        }

        return strengthArray;
    }

    public static double GetPingStrengthAtPoint(QuadTree<QuadTreePayload> tree, LLPoint point, double cellWidthDegs)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (cellWidthDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(cellWidthDegs), "Cell width must be greater than zero.");
        }

        if (!tree.Root.Bounds.Contains(point))
        {
            throw new ArgumentOutOfRangeException(nameof(point), "Point must be inside the tree bounds.");
        }

        tree.EnterReadLock();
        try
        {
            var nodeIndex = tree.RootIndex;

            // Descend only until the node is comparable to the requested query cell size.
            while (tree.GetNode(nodeIndex).Bounds.MaxSpanDegrees > cellWidthDegs)
            {
                var childIndex = tree.GetChildContaining(nodeIndex, point);
                if (childIndex == QuadTreeNode<QuadTreePayload>.EmptyIndex)
                {
                    break;
                }

                nodeIndex = childIndex;
            }

            return AccumulateStrengthFromNode(tree, nodeIndex);
        }
        finally
        {
            tree.ExitReadLock();
        }
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Accumulation / Decay
    // --------------------------------------------------------------------------------------------

    public static int EnsureNodeAtPosition<TPayload>(QuadTree<TPayload> tree, QuadTreePosition position)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var nodeIndex = tree.RootIndex;

        foreach (var quadrantDigit in position.Path)
        {
            nodeIndex = tree.EnsureChildIndex(nodeIndex, quadrantDigit);
        }

        return nodeIndex;
    }

    public static (int NodeIndex, QuadTreePosition ResolvedPosition, bool HasExactNode) NavigateToExistingNode<TPayload>(QuadTree<TPayload> tree, QuadTreePosition position)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var nodeIndex = tree.RootIndex;
        var resolvedPosition = QuadTreePosition.Root;

        foreach (var quadrantDigit in position.Path)
        {
            var childIndex = tree.GetChildIndex(nodeIndex, quadrantDigit);
            if (childIndex == QuadTreeNode<TPayload>.EmptyIndex)
            {
                return (nodeIndex, resolvedPosition, false);
            }

            nodeIndex = childIndex;
            resolvedPosition = resolvedPosition.Append(quadrantDigit);
        }

        return (nodeIndex, resolvedPosition, true);
    }

    public static double AccumulateStrengthFromNode(QuadTree<QuadTreePayload> tree, int nodeIndex)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var node = tree.GetNode(nodeIndex);
        double totalStrength = node.HasPayload ? node.Payload.GetCurrentValue() : 0d;

        if (node.TopLeftIndex != QuadTreeNode<QuadTreePayload>.EmptyIndex)
        {
            totalStrength += AccumulateStrengthFromNode(tree, node.TopLeftIndex);
            totalStrength += AccumulateStrengthFromNode(tree, node.TopRightIndex);
            totalStrength += AccumulateStrengthFromNode(tree, node.BottomLeftIndex);
            totalStrength += AccumulateStrengthFromNode(tree, node.BottomRightIndex);
        }

        return totalStrength;
    }

    // Usage: QuadTreeNavigation.DecayPings(tree, pingNodeList);
    public static void DecayPings(QuadTree<QuadTreePayload> tree, List<int> pingNodeList)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(pingNodeList);

        if (pingNodeList.Count == 0)
        {
            return;
        }

        tree.EnterWriteLock();
        try
        {
            foreach (var nodeIndex in pingNodeList)
            {
                var node = tree.GetNode(nodeIndex);
                if (!node.HasPayload)
                {
                    continue;
                }

                if (node.Payload.GetCurrentValue() > 0.0001d)
                {
                    continue;
                }

                node.Payload = default;
                node.HasPayload = false;

                // Walk back up the tree and prune branches that no longer contain any strength.
                var currentNodeIndex = nodeIndex;
                while (tree.GetNode(currentNodeIndex).ParentIndex != QuadTreeNode<QuadTreePayload>.EmptyIndex)
                {
                    var parentNodeIndex = tree.GetNode(currentNodeIndex).ParentIndex;
                    var parentNode = tree.GetNode(parentNodeIndex);

                    if (parentNode.TopLeftIndex == QuadTreeNode<QuadTreePayload>.EmptyIndex)
                    {
                        break;
                    }

                    var hasRemainingStrength =
                        AccumulateStrengthFromNode(tree, parentNode.TopLeftIndex) > 0d ||
                        AccumulateStrengthFromNode(tree, parentNode.TopRightIndex) > 0d ||
                        AccumulateStrengthFromNode(tree, parentNode.BottomLeftIndex) > 0d ||
                        AccumulateStrengthFromNode(tree, parentNode.BottomRightIndex) > 0d;

                    if (hasRemainingStrength)
                    {
                        break;
                    }

                    tree.ClearChildren(parentNodeIndex);
                    currentNodeIndex = parentNodeIndex;
                }
            }
        }
        finally
        {
            tree.ExitWriteLock();
        }
    }
}
