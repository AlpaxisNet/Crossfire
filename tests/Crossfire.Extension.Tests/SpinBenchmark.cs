// Not ported from a Crossfire file: a benchmark of the worker's spin (docs/extension.md, "The spin"). It asserts nothing about time and
// does nothing unless the environment variable CROSSFIRE_BENCH is set: it is run by hand and its figures are read from the output.
//
//   CROSSFIRE_BENCH=1 dotnet test tests/Crossfire.Extension.Tests -c Release --filter SpinBenchmark --logger "console;verbosity=detailed"
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using Crossfire.Extension.Protocol;
using Xunit.Abstractions;

namespace Crossfire.Extension.Tests;

public class SpinBenchmark
{
    private readonly ITestOutputHelper output;

    public SpinBenchmark(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static double[] Run(TimeSpan spin, int calls, int gapMicroseconds)
    {
        using TempFolder world = TestWorld.Make();
        var options = new ExtensionOptions { Spin = spin, Writer = new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) } };
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, options);
        host.Init();
        Val[] args = { Val.Str("sword") };
        var micros = new double[calls];
        for (int i = 0; i < calls + 200; i++)
        {
            long start = Stopwatch.GetTimestamp();
            host.Call(Manifest.OpFindArchetype, args);
            long took = Stopwatch.GetTimestamp() - start;
            if (i >= 200)
            {
                micros[i - 200] = took * 1_000_000.0 / Stopwatch.Frequency;
            }
            if (gapMicroseconds > 0)
            {
                long until = Stopwatch.GetTimestamp() + gapMicroseconds * Stopwatch.Frequency / 1_000_000;
                while (Stopwatch.GetTimestamp() < until)
                {
                }
            }
        }
        Array.Sort(micros);
        return micros;
    }

    [Fact]
    public void Spin_Given_chains_and_gaps_When_calls_are_timed_Then_the_figures_are_printed()
    {
        if (Environment.GetEnvironmentVariable("CROSSFIRE_BENCH") == null)
        {
            return;
        }
        foreach (int gap in new[] { 0, 20, 100, 1000 })
        {
            foreach (int spinMicroseconds in new[] { 0, 50 })
            {
                double[] m = Run(TimeSpan.FromTicks(spinMicroseconds * 10L), 4000, gap);
                output.WriteLine($"gap {gap,5} us  spin {spinMicroseconds,2} us:  median {m[m.Length / 2],7:F1} us  p90 {m[m.Length * 9 / 10],7:F1} us  p99 {m[m.Length * 99 / 100],7:F1} us  mean {m.Average(),7:F1} us");
            }
        }
    }
}
