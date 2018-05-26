using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using Meziantou.Framework;
using Meziantou.Framework.Win32;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Win32;

namespace UnlockFile
{
    internal static class Program
    {
        private const string Help = "-?|-h|--help";

        private static int Main(string[] args)
        {
            var app = new CommandLineApplication()
            {
                Name = "UnlockFile",
                FullName = "UnlockFile",
                Description = "UnlockFile allows to unlock a file by shutting down the applications that lock the file"
            };

            app.HelpOption(Help);
            app.VersionOption("--version", "1.0.0");

            AddRegisterShellMenuCommand(app);
            AddUnregisterShellMenuCommand(app);
            UnlockFileCommand(args, app);

            try
            {
                app.Execute(args);
                return 0;
            }
            catch (CommandParsingException ex)
            {
                Console.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }

        private static void UnlockFileCommand(string[] args, CommandLineApplication app)
        {
            var attachDebuggerOption = app.Option("--debugger", "Launch debugger", CommandOptionType.NoValue);
            var runAsAdminOption = app.Option("--run-as-admin", "Run UnlockFile as administrator", CommandOptionType.NoValue);
            var pauseOption = app.Option("--pause", "Wait for user input to close the process", CommandOptionType.NoValue);
            var pathsArgument = app.Argument("PATH", "Files or folders to unlock", multipleValues: true);
            app.OnExecute(() =>
            {
                if (attachDebuggerOption.HasValue())
                {
                    while (!Debugger.IsAttached)
                    {
                        Debugger.Launch();
                    }
                }

                if (runAsAdminOption.HasValue())
                {
                    using (var token = AccessToken.OpenCurrentProcessToken(TokenAccessLevels.Query))
                    {
                        if (token.GetElevationType() == TokenElevationType.Limited)
                        {
                            // We cannot just use UnlockFile as the filename because while developping the command line looks like "dotnet exe UnlockFile.dll args"
                            string commandLine;
                            using (var searcher = new ManagementObjectSearcher("SELECT Name, CommandLine FROM Win32_Process WHERE ProcessId = " + Process.GetCurrentProcess().Id))
                            using (var objects = searcher.Get())
                            {
                                commandLine = objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
                            }

                            var (fileName, arguments) = SplitCommandLine(commandLine);


                            var psi = new ProcessStartInfo();
                            psi.FileName = fileName;
                            psi.Arguments = arguments.Replace("--run-as-admin", "", StringComparison.OrdinalIgnoreCase);
                            psi.Verb = "runas";
                            psi.UseShellExecute = true;
                            Process.Start(psi);

                            return 0;
                        }
                    }
                }

                var paths = pathsArgument.Values.Where(IsPath).ToArray();
                if (paths.Length == 0)
                {
                    app.ShowHelp();
                    return 1;
                }

                using (var session = RestartManager.CreateSession())
                {
                    session.RegisterFiles(paths);
                    var processes = session.GetProcessesLockingResources();
                    if (processes.Count == 0)
                    {
                        Console.WriteLine("Files or folders are not locked by a process");
                    }
                    else
                    {
                        foreach (var process in processes)
                        {
                            Console.WriteLine($"{process.ProcessName} ({process.Id})");
                        }

                        if (Prompt.YesNo("Do you want to close the processes?", false))
                        {
                            session.Shutdown(RmShutdownType.RmForceShutdown);
                        }
                    }
                }

                if (pauseOption.HasValue())
                {
                    Console.ReadLine();
                }

                return 0;
            });
        }

        private static void AddUnregisterShellMenuCommand(CommandLineApplication app)
        {
            app.Command("UnregisterShellMenu", cmd =>
            {
                cmd.HelpOption(Help);
                cmd.OnExecute(() =>
                {
                    Registry.CurrentUser.DeleteSubKeyTree("Software\\Classes\\*\\UnlockFile", throwOnMissingSubKey: false);
                    return 0;
                });
            });
        }

        private static void AddRegisterShellMenuCommand(CommandLineApplication app)
        {
            app.Command("RegisterShellMenu", cmd =>
            {
                var runAsAdmin = cmd.Option("--run-as-admin", "Run UnlockFile as administrator", CommandOptionType.NoValue);
                cmd.HelpOption(Help);

                cmd.OnExecute(() =>
                {
                    using (var reg = Registry.CurrentUser.CreateSubKey("Software\\Classes\\*\\UnlockFile", writable: true))
                    {
                        reg.SetValue("", "Unlock File");

                        using (var command = reg.CreateSubKey("command"))
                        {
                            var value = "UnlockFile \"%1\"";
                            if (runAsAdmin.HasValue())
                            {
                                value += " --run-as-admin";
                            }

                            reg.SetValue("", value);
                        }
                    }

                    return 0;
                });
            });
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

        private static (string fileName, string arguments) SplitCommandLine(string command)
        {
            var isQuoted = false;
            var i = 0;
            for (; i < command.Length; i++)
            {
                switch (command[i])
                {
                    case ' ':
                        if (!isQuoted)
                        {
                            return (command.Substring(0, i).Trim('"'), command.Substring(i).TrimStart());
                        }
                        break;

                    case '"':
                        isQuoted = !isQuoted;
                        break;
                }
            }

            return (command.Substring(0, i).Trim('"'), command.Substring(i));
        }
    }
}
