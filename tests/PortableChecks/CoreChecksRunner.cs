using System;
using System.IO;

public static class CoreChecksRunner
{
    public static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: dotnet run --project tests/PortableChecks -- [report-path]");
            return 2;
        }

        try
        {
            string report = Path.GetFullPath(args.Length == 1
                ? args[0]
                : Path.Combine("work", "portable-checks", "report.txt"));
            Console.WriteLine("Portable domain checks use a System.Text.Json adapter; Unity-native JSON and UI require separate validation.");
            MochiDay.CoreChecks.Run(report);
            Console.WriteLine("Report: " + report);
            return MochiDay.CoreChecks.Failures == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Could not complete portable checks: " + error);
            return 2;
        }
    }
}
