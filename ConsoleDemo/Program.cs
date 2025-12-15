using System;
using System.Collections.Generic;
using TRC.Core;

namespace TRC.ConsoleDemo;

internal static class Program
{
    static void Main()
    {
        Console.WriteLine("TRC Console Demo (skeleton).");
        Console.WriteLine("Feeding synthetic 5m bars into TRC engine...\n");

        var engine = new TrcEngine();

        // In a real setup, replace this with:
        // var bars = LoadFromCsv("yourfile.csv");
        var bars = GenerateSyntheticBars();

        foreach (var bar in bars)
        {
            int htfBias = GetDummyHtfBias(bar.Time);

            var events = engine.OnBar(bar, htfBias);

            foreach (var e in events)
            {
                Console.WriteLine($"{e.Time:u} | {e.Type,-15} | info: {FormatInfo(e.Info)}");
            }
        }

        Console.WriteLine("\nDone. Press any key to exit.");
        Console.ReadKey();
    }

    // Very naive placeholder HTF bias function
    // Replace with proper 4H/1H/15M logic
    private static int GetDummyHtfBias(DateTime time)
    {
        // Example: alternate bias each hour block just to trigger both sides
        return (time.Hour % 2 == 0) ? 1 : -1;
    }

    private static string FormatInfo(IReadOnlyDictionary<string, object?> info)
    {
        var parts = new List<string>();
        foreach (var kvp in info)
        {
            parts.Add($"{kvp.Key}={kvp.Value}");
        }

        return string.Join(", ", parts);
    }

    private static IReadOnlyList<Bar> GenerateSyntheticBars()
    {
        var list = new List<Bar>();
        var rand = new Random(42);

        DateTime start = new DateTime(2025, 1, 1, 9, 0, 0);

        decimal price = 100m;

        for (int i = 0; i < 500; i++)
        {
            // Simple random walk with mild trend bias
            decimal delta = (decimal)(rand.NextDouble() - 0.5) * 0.5m;
            price += delta;

            decimal high = price + 0.2m;
            decimal low  = price - 0.2m;
            decimal open = price - delta * 0.3m;
            decimal close = price;

            var bar = new Bar(
                Time: start.AddMinutes(5 * i),
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: 0m
            );

            list.Add(bar);
        }

        return list;
    }
}
