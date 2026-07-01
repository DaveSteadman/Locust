using Locust.Spatial;

namespace Locust;

internal static class Program
{
    private static readonly QuadTree GlobalTree = new();

    private static void Main()
    {
        // Ping a LLPoint with roughly 10 m angular resolution and print the containing node.
        var ping = new LLPing(new LLPoint(20d, 20d), 1, 0.0001, 1);
        var containingNode = PingOperations.Apply(GlobalTree, ping);


        List<QuadTreeNode> pingNodeList = new ();
        pingNodeList.Add(containingNode);


        //Console.WriteLine($"Ping bounds: {ping.Bounds}");
        Console.WriteLine($"Containing node bounds: {containingNode.Bounds}");

        // print each node level on the way to the ping's containing node, and layer number
        var node = GlobalTree.Root;
        int layernumber = 0;

        while (node != containingNode)
        {
            Console.WriteLine($"Layer: {layernumber:D2} // Node bounds: {node.Bounds}");
            node = node.EnsureChildContaining(ping.Center);
            layernumber++;
        }

        // extract a 2d array of ping strengths for a given bounding box and resolution
        var bounds = new LLRect(19.9999, 19.9999, 0.0002, 0.0002);
        int widthcount  = 10;
        int heightcount = 10;
        var strengthArray = QuadTreeNavigation.StrengthValuesToArray(GlobalTree, bounds, widthcount, heightcount);

        // write the strength array to csv file
        var csvFilePath = "ping_strengths.csv";


        KoreNumeric2DArrayIO<double>.SaveToCSVFile(strengthArray, csvFilePath, 2);
        Console.WriteLine($"Ping strengths written to {csvFilePath}");


    }
}
