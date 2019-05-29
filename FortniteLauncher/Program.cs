using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FortniteLauncher
{
    class Program
    {
        private const string FORTNITE_EXECUTABLE = "FortniteClient-Win64-Shipping.exe";

        private static Process _fnProcess;
        private static Patcher _fnPatcher;

        static void Main(string[] args)
        {
            string joinedArgs = string.Join(" ", args);

            // Check if -FORCECONSOLE exists in args (regardless of case) to force console (due to Epic Games Launcher by default hiding it)
            if (joinedArgs.ToUpper().Contains("-FORCECONSOLE"))
            {
                joinedArgs = Regex.Replace(joinedArgs, "-FORCECONSOLE", string.Empty, RegexOptions.IgnoreCase);
                new Process
                {
                    StartInfo =
                    {
                        FileName        = Path.GetFileName(Assembly.GetEntryAssembly().Location),
                        Arguments       = joinedArgs,
                        UseShellExecute = false
                    }
                }.Start();

                Environment.Exit(0);
            }

            // Check if the Fortnite client exists in the current work path.
            if (!File.Exists(FORTNITE_EXECUTABLE))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\"{FORTNITE_EXECUTABLE}\" is missing!");
                Console.ReadKey();
                Environment.Exit(1);
            }

            // Check if -NOSSLPINNING exists in args (regardless of case) to disable SSL pinning
            if (joinedArgs.ToUpper().Contains("-NOSSLPINNING"))
            {
                joinedArgs           = Regex.Replace(joinedArgs, "-NOSSLPINNING", string.Empty, RegexOptions.IgnoreCase);
                Patcher.noSslPinning = true;
            }

            // Setup a process exit event handler
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Initialize Fortnite process with start info
            _fnProcess = new Process
            {
                StartInfo =
                {
                    FileName               = FORTNITE_EXECUTABLE,
                    Arguments              = $"{joinedArgs} -noeac -nobe -fltoken=none",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false
                }
            };

            _fnProcess.Start(); // Start Fortnite client process

            // Set up our async readers
            AsyncStreamReader asyncOutputReader = new AsyncStreamReader(_fnProcess.StandardOutput);
            AsyncStreamReader asyncErrorReader  = new AsyncStreamReader(_fnProcess.StandardError);

            asyncOutputReader.DataReceived += delegate (object sender, string data)
            {
                Console.ForegroundColor = ConsoleColor.White;

                string formattedData = data.ToUpper().Replace(" ", "_"); // Convert data to all uppercase characters and replace spaces with "_"

                // Check if formatted data contains "ASYNC_LOADING_INITIALIZED", if so, initalize the patcher (because we have to wait for Fortnite to be fully loaded into memory)
                if (formattedData.Contains("ASYNC_LOADING_INITIALIZED") && _fnPatcher == null) _fnPatcher = new Patcher(_fnProcess);

                // Check if formatted data contains "STARTING_UPDATE_CHECK", if so, run the patcher (because Fortnite internal AC sucks!)
                if (formattedData.Contains("STARTING_UPDATE_CHECK") && _fnPatcher != null) _fnPatcher.Run();

                Console.WriteLine(data);
            };

            asyncErrorReader.DataReceived += delegate (object sender, string data)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(data);
            };

            // Start our async readers
            asyncOutputReader.Start();
            asyncErrorReader.Start();

            _fnProcess.WaitForExit(); // We'll wait for the Fortnite process to exit, otherwise our launcher will just close instantly
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            if (!_fnProcess.HasExited) _fnProcess.Kill();
        }
    }
}
