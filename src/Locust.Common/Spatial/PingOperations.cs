namespace Locust;

public static class PingOperations
{
    // --------------------------------------------------------------------------------------------
    // MARK: Apply
    // --------------------------------------------------------------------------------------------

    public static int Apply(QuadTree<QuadTreePayload> tree, LLPing ping)
    {
        if (ping.RadiusDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(ping), "Ping radius must be greater than zero.");
        }

        return QuadTreeNavigation.NavigateToContainingNode(tree, ping);
    }
}
