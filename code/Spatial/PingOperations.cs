namespace Locust.Spatial;

public static class PingOperations
{
    public static QuadTreeNode Apply(QuadTree tree, LLPing ping)
    {
        if (ping.RadiusDegs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(ping), "Ping radius must be greater than zero.");
        }

        return QuadTreeNavigation.NavigateToContainingNode(tree, ping);
    }
}
