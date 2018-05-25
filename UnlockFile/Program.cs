using System;
using System.IO;
using System.Linq;
using Meziantou.Framework.Win32;

namespace UnlockFile
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var paths = args.Where(IsPath).ToArray();
            if (paths.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            using (var session = RestartManager.CreateSession())
            {
                session.RegisterFiles(paths);
                var processes = session.GetProcessesLockingResources();
                if (processes.Count == 0)
                {
                    Console.WriteLine("Not locked by a process");
                    return 0;
                }

                foreach (var process in processes)
                {
                    Console.WriteLine($"{process.ProcessName} ({process.Id})");
                }

                Console.WriteLine("Do you want to close the processes? [y/N]");
                var key = Console.ReadKey();
                if (char.ToLowerInvariant(key.KeyChar) == 'y')
                {
                    session.Shutdown(RmShutdownType.RmForceShutdown);
                    Console.WriteLine("Do you want to restart the processes? [y/N]");
                    key = Console.ReadKey();
                    if (char.ToLowerInvariant(key.KeyChar) == 'y')
                    {
                        session.Restart();
                    }
                }
            }

            return 0;
        }

        private static bool IsPath(string path)
        {
            try
            {
                return File.Exists(path) || Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: UnlockFile [path] [path] ...");
        }
    }
}
