namespace Locust.Spatial;

public static class QuadTreeNavigation
{
    public static QuadTreeNode NavigateToContainingNode(QuadTree tree, LLPing ping)
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
            var node = tree.Root;
            while (node.Bounds.MaxSpanDegrees > ping.RadiusDegs)
            {
                node = node.EnsureChildContaining(ping.Center);
            }

            node.NodePing = new KoreMovingDouble(ping.Strength, 0d, ping.DecaySecs);

            return node;
        }
        finally
        {
            tree.ExitWriteLock();
        }
    }

    // --------------------------------------------------------------------------------------------

    public static KoreDouble2DArray StrengthValuesToArray(QuadTree tree, LLRect bounds, int widthcount, int heightcount)
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

        KoreDouble2DArray strengthArray = new (widthcount, heightcount);

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

    // --------------------------------------------------------------------------------------------

    public static double GetPingStrengthAtPoint(QuadTree tree, LLPoint point, double cellWidthDegs)
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
            var node = tree.Root;

            // Descend only until the node is comparable to the requested query cell size.
            while (node.Bounds.MaxSpanDegrees > cellWidthDegs)
            {
                var child = node.GetChildContaining(point);
                if (child is null)
                {
                    break;
                }
                node = child;
            }

            return AccumulateStrengthFromNode(node);
        }
        finally
        {
            tree.ExitReadLock();
        }
    }

    // --------------------------------------------------------------------------------------------

    public static double AccumulateStrengthFromNode(QuadTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        double totalStrength = node.NodePing?.CurrentValue ?? 0d;

        foreach (var child in node.Children)
        {
            totalStrength += AccumulateStrengthFromNode(child);
        }

        return totalStrength;
    }



    // --------------------------------------------------------------------------------------------

    public static void DecayPings(List<QuadTreeNode> pingNodeList)
    {
        ArgumentNullException.ThrowIfNull(pingNodeList);

        foreach (var node in pingNodeList)
        {
            if (node.NodePing is not null)
            {
                if (node.NodePing.IsZero)
                {
                    // Loop through the parent node levels, and prune the node at the point where all children have decayed to zero strength.
                    // Using AccumulateStrengthFromNode to accumulate the strength of all children
                    var currentNode = node;

                    while (currentNode.Parent is not null)
                    {
                        var parentNode = currentNode.Parent;
                        bool allChildrenZero = true;

                        foreach (var sibling in parentNode.Children)
                        {
                            if (AccumulateStrengthFromNode(sibling) > 0d)
                            {
                                allChildrenZero = false;
                                break;
                            }
                        }

                        if (allChildrenZero)
                        {
                            parentNode.ClearChildren();
                            currentNode = parentNode;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }
    }


}
