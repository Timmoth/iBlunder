using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Sapling.Engine;
using Sapling.Engine.DataGen;
using Sapling.Engine.Evaluation;
using Sapling.Engine.MoveGen;
using Sapling.Engine.Search;
using Sapling.Engine.Tuning;

namespace Sapling;

public static class UciOptions
{
    public static bool IsDebug = false;
}
internal class Program
{
    private static readonly ConcurrentQueue<string> CommandQueue = new();
    private static readonly ManualResetEventSlim CommandAvailable = new(false);
    private static bool hasQuit = false;

    private static FileStream _fileStream;
    private static StreamWriter _logWriter;

private static void Main(string[] args)
    {
        Console.SetIn(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8, false, 2048 * 4));

        if (args.Length > 0 && args[0] == "--version")
        {
            // Get the version from the assembly information
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"{version.Major}-{version.Minor}-{version.Build}");
            return;
        }

#if AVX512
            if (!Avx512BW.IsSupported)
            {
                Console.WriteLine("[Error] Avx512BW is not supported on this system");
                return;
            }
#else
        if (!Avx2.IsSupported)
            {
                Console.WriteLine("[Error] Avx2 is not supported on this system");
                return;
            }
        #endif

        if (!Bmi1.IsSupported)
        {
            Console.WriteLine("[Error] Bmi1 is not supported on this system");
            return;
        }

        if (!Bmi2.IsSupported)
        {
            Console.WriteLine("[Error] Bmi2 is not supported on this system");
            return;
        }

        if (!Popcnt.IsSupported)
        {
            Console.WriteLine("[Error] Popcnt is not supported on this system");
            return;
        }

        if (!Sse.IsSupported)
        {
            Console.WriteLine("[Error] Sse is not supported on this system");
            return;
        }
        var logDirectory = Path.Combine(Environment.CurrentDirectory, "logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var fileName = (DateTime.Now.ToString("g") + Guid.NewGuid()).Replace("/", "-").Replace(" ", "_")
            .Replace(":", "-");
        var logFilePath = Path.Combine(logDirectory, $"{fileName}.txt");
        _fileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write);
        _logWriter = new StreamWriter(_fileStream);


        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            // Log the exception or take appropriate action
            Console.WriteLine("Unhandled Exception: " + ((Exception)e.ExceptionObject).Message);
            _logWriter.WriteLine("Unhandled Exception: " + ((Exception)e.ExceptionObject).Message);
            _logWriter.Flush();
            _logWriter.Close();

        };

        // Force the static constructors to be called
        var tasks = new[]
        {
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(NnueWeights).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(NnueExtensions).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AttackTables).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PieceValues).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RepetitionDetector).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(HistoryHeuristicExtensions).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PVTable).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Zobrist).TypeHandle)),
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SpsaOptions).TypeHandle))
        };

        // Wait for all tasks to complete
        Task.WaitAll(tasks);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();

        if (args.Contains("bench"))
        {
            Bench.Run();
            return;
        }

        UciOptions.IsDebug = args.Contains("debug");

      
        try
        {
            UciEngine engine = new(_logWriter);

            // Start the command reading task
            _ = Task.Run(() =>
            {
                ReadCommands(engine);
            });

            // Process commands in the main loop
            ProcessCommands(engine);
        }
        catch (Exception ex)
        {
            _logWriter.WriteLine("[FATAL ERROR]");
            _logWriter.WriteLine("----------");
            _logWriter.WriteLine(ex.ToString());
            _logWriter.WriteLine("----------");
        }
        finally
        {
            _logWriter.Flush();
            _logWriter.Flush();
            _logWriter.Close();
        }
    }

    private static void ReadCommands(UciEngine engine)
    {
        while (true)
        {
            var command = Console.ReadLine();
            if (string.IsNullOrEmpty(command))
            {
                continue; // Skip empty commands
            }

            if (command.Contains("quit", StringComparison.OrdinalIgnoreCase))
            {
                hasQuit = true;
                engine.ReceiveCommand("stop");
                Environment.Exit(0);
                break;
            }

            if (command.Contains("stop", StringComparison.OrdinalIgnoreCase))
            {
                // Process the stop command immediately
                engine.ReceiveCommand(command);
                continue;
            }

            CommandQueue.Enqueue(command);
            CommandAvailable.Set(); // Signal that a command is available
        }
    }

    private static void ProcessCommands(UciEngine engine)
    {
        while (!hasQuit)
        {
            CommandAvailable.Wait(); // Wait until a command is available
            CommandAvailable.Reset(); // Reset the event for the next wait

            while (CommandQueue.TryDequeue(out var command))
            {
                engine.ReceiveCommand(command);
            }
        }
    }
}