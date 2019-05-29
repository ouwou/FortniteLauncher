using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FortniteLauncher
{
    class Patcher
    {
        private bool _runOnce;

        private Process _fnProcess;
        private IntPtr  _fnHandle;
        private SigScan _sigScan;

        // NoSSLPinning
        public static bool      noSslPinning;
        private IntPtr          _verifyPeerAddress;
        private readonly byte[] _verifyPeerPatched = { 0x41, 0x39, 0x28, 0xB0, 0x00, 0x90, 0x88, 0x83, 0x50, 0x04, 0x00, 0x00 };

        public Patcher(Process fnProcess)
        {
            _fnProcess = fnProcess;
            _fnHandle  = Win32.OpenProcess(Win32.PROCESS_ALL_ACCESS, false, _fnProcess.Id);
            _sigScan   = new SigScan(_fnHandle);

            _sigScan.SelectModule(_fnProcess.MainModule);

            PrefetchAddresses();
        }

        public void Run()
        {
            if (_runOnce || _fnHandle == IntPtr.Zero) return;

            // Run patches
            NoSSLPinning();

            _runOnce = true; // Make sure we can't trigger Run() again
        }

        private void PrefetchAddresses()
        {
            if (_runOnce || _fnHandle == IntPtr.Zero) return;

            if (noSslPinning) _sigScan.AddPattern("CURLOPT_SSL_VERIFYPEER", "41 39 28 0F 95 C0 88 83 50 04 00 00");

            Dictionary<string, ulong> allPatterns = _sigScan.FindPatterns(out long lTime);

#if DEBUG
            Console.WriteLine($"Took {lTime}ms to find {allPatterns.Count} pattern(s) in memory.");
            Console.WriteLine();
#endif

            if (noSslPinning) _verifyPeerAddress = (IntPtr)allPatterns["CURLOPT_SSL_VERIFYPEER"];
        }

        private void NoSSLPinning()
        {
            if (noSslPinning && _verifyPeerAddress != IntPtr.Zero)
            {
                try
                {
                    Win32.WriteProcessMemory(_fnHandle, _verifyPeerAddress, _verifyPeerPatched, _verifyPeerPatched.Length, out IntPtr bytesWritten); // Write patched CURLOPT_SSL_VERIFYPEER code

#if DEBUG
                    // Log how many bytes we wrote
                    Console.WriteLine($"Patched {bytesWritten} byte(s) in CURLOPT_SSL_VERIFYPEER.");
                    Console.WriteLine();
#endif
                }
                catch (Exception e)
                {
#if DEBUG
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"An error has occured while attempting to patch CURLOPT_SSL_VERIFYPEER. Message = {e.Message}");
                    Console.WriteLine();
#endif
                }

                noSslPinning = false; // Set noSslPinning to false to make sure we don't trigger this again!
            }
        }
    }
}
