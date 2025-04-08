using Genymobile.Gnirehtet.Relay;
using Gnirehtet.Relay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// 注意：假设外部依赖类（Log, Relay, CommandExecutionException 等）需要单独实现
namespace Gnirehtet
{
    public static class MainClass
    {
        private const string TAG = "Gnirehtet";
        private static readonly string NL = Environment.NewLine;
        private const string REQUIRED_APK_VERSION_CODE = "9";


        private static string GetAdbPath()
        {
            string adb = Environment.GetEnvironmentVariable("ADB");
            return adb ?? "adb";
        }

        private static string GetApkPath()
        {
            string apk = Environment.GetEnvironmentVariable("GNIREHTET_APK");
            return apk ?? "gnirehtet.apk";
        }

        public enum Command
        {
            INSTALL = CommandLineArguments.PARAM_SERIAL,
            UNINSTALL = CommandLineArguments.PARAM_SERIAL,
            REINSTALL = CommandLineArguments.PARAM_SERIAL,
            RUN = CommandLineArguments.PARAM_SERIAL | CommandLineArguments.PARAM_DNS_SERVER | CommandLineArguments.PARAM_ROUTES | CommandLineArguments.PARAM_PORT,
            AUTORUN = CommandLineArguments.PARAM_DNS_SERVER | CommandLineArguments.PARAM_ROUTES | CommandLineArguments.PARAM_PORT,
            START = CommandLineArguments.PARAM_SERIAL | CommandLineArguments.PARAM_DNS_SERVER | CommandLineArguments.PARAM_ROUTES | CommandLineArguments.PARAM_PORT,
            AUTOSTART = CommandLineArguments.PARAM_DNS_SERVER | CommandLineArguments.PARAM_ROUTES | CommandLineArguments.PARAM_PORT,
            STOP = CommandLineArguments.PARAM_SERIAL,
            RESTART = CommandLineArguments.PARAM_SERIAL | CommandLineArguments.PARAM_DNS_SERVER | CommandLineArguments.PARAM_ROUTES | CommandLineArguments.PARAM_PORT,
            TUNNEL = CommandLineArguments.PARAM_SERIAL | CommandLineArguments.PARAM_PORT,
            RELAY = CommandLineArguments.PARAM_PORT
        }

        private static readonly Dictionary<Command, (string Name, Func<string> Description, Action<CommandLineArguments> Execute)> CommandDefinitions =
            new Dictionary<Command, (string, Func<string>, Action<CommandLineArguments>)>
            {
                { Command.INSTALL, ("install", () => "Install the client on the Android device and exit.\n" +
                                                    "If several devices are connected via adb, then serial must be\n" +
                                                    "specified.", args => CmdInstall(args.Serial)) },
                { Command.UNINSTALL, ("uninstall", () => "Uninstall the client from the Android device and exit.\n" +
                                                        "If several devices are connected via adb, then serial must be\n" +
                                                        "specified.", args => CmdUninstall(args.Serial)) },
                { Command.REINSTALL, ("reinstall", () => "Uninstall then install.", args => CmdReinstall(args.Serial)) },
                { Command.RUN, ("run", () => "Enable reverse tethering for exactly one device:\n" +
                                            "  - install the client if necessary;\n" +
                                            "  - start the client;\n" +
                                            "  - start the relay server;\n" +
                                            "  - on Ctrl+C, stop both the relay server and the client.", args => CmdRun(args.Serial, args.DnsServers, args.Routes, args.Port)) },
                { Command.AUTORUN, ("autorun", () => "Enable reverse tethering for all devices:\n" +
                                                    "  - monitor devices and start clients (autostart);\n" +
                                                    "  - start the relay server.", args => CmdAutorun(args.DnsServers, args.Routes, args.Port)) },
                { Command.START, ("start", () => "Start a client on the Android device and exit.\n" +
                                                "If several devices are connected via adb, then serial must be\n" +
                                                "specified.\n" +
                                                "If -d is given, then make the Android device use the specified\n" +
                                                "DNS server(s). Otherwise, use 8.8.8.8 (Google public DNS).\n" +
                                                "If -r is given, then only reverse tether the specified routes.\n" +
                                                "If -p is given, then make the relay server listen on the specified\n" +
                                                "port. Otherwise, use port 31416.\n" +
                                                "Otherwise, use 0.0.0.0/0 (redirect the whole traffic).\n" +
                                                "If the client is already started, then do nothing, and ignore\n" +
                                                "the other parameters.\n" +
                                                "10.0.2.2 is mapped to the host 'localhost'.", args => CmdStart(args.Serial, args.DnsServers, args.Routes, args.Port)) },
                { Command.AUTOSTART, ("autostart", () => "Listen for device connexions and start a client on every detected\n" +
                                                        "device.\n" +
                                                        "Accept the same parameters as the start command (excluding the\n" +
                                                        "serial, which will be taken from the detected device).", args => CmdAutostart(args.DnsServers, args.Routes, args.Port)) },
                { Command.STOP, ("stop", () => "Stop the client on the Android device and exit.\n" +
                                              "If several devices are connected via adb, then serial must be\n" +
                                              "specified.", args => CmdStop(args.Serial)) },
                { Command.RESTART, ("restart", () => "Stop then start.", args => CmdRestart(args.Serial, args.DnsServers, args.Routes, args.Port)) },
                { Command.TUNNEL, ("tunnel", () => "Set up the 'adb reverse' tunnel.\n" +
                                                  "If a device is unplugged then plugged back while gnirehtet is\n" +
                                                  "active, resetting the tunnel is sufficient to get the\n" +
                                                  "connection back.", args => CmdTunnel(args.Serial, args.Port)) },
                { Command.RELAY, ("relay", () => "Start the relay server in the current terminal.", args => CmdRelay(args.Port)) }
            };

        private static void CmdInstall(string serial)
        {
            Log.I(TAG, "Installing gnirehtet client...");
            ExecAdb(serial, "install", "-r", GetApkPath());
        }

        private static void CmdUninstall(string serial)
        {
            Log.I(TAG, "Uninstalling gnirehtet client...");
            ExecAdb(serial, "uninstall", "com.genymobile.gnirehtet");
        }

        private static void CmdReinstall(string serial)
        {
            CmdUninstall(serial);
            CmdInstall(serial);
        }

        private static void CmdRun(string serial, string dnsServers, string routes, int port)
        {
            AsyncStart(serial, dnsServers, routes, port);

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    CmdStop(serial);
                }
                catch (Exception ex)
                {
                    Log.E(TAG, "Cannot stop client", ex);
                }
            };

            CmdRelay(port);
        }

        private static void CmdAutorun(string dnsServers, string routes, int port)
        {
            new Thread(() =>
            {
                try
                {
                    CmdAutostart(dnsServers, routes, port);
                }
                catch (Exception ex)
                {
                    Log.E(TAG, "Cannot auto start clients", ex);
                }
            }).Start();

            CmdRelay(port);
        }

        private static void CmdStart(string serial, string dnsServers, string routes, int port)
        {
            if (MustInstallClient(serial))
            {
                CmdInstall(serial);
                Thread.Sleep(500); // ms
            }

            Log.I(TAG, "Starting client...");
            CmdTunnel(serial, port);

            var cmd = new List<string> { "shell", "am", "start", "-a", "com.genymobile.gnirehtet.START", "-n", "com.genymobile.gnirehtet/.GnirehtetActivity" };
            if (!string.IsNullOrEmpty(dnsServers))
            {
                cmd.AddRange(new[] { "--esa", "dnsServers", dnsServers });
            }
            if (!string.IsNullOrEmpty(routes))
            {
                cmd.AddRange(new[] { "--esa", "routes", routes });
            }
            ExecAdb(serial, cmd);
        }

        private static void CmdAutostart(string dnsServers, string routes, int port)
        {
            var adbMonitor = new AdbMonitor(new AdbMonitor.IAdbDevicesCallback
            {
                OnDeviceDetected = (serial) =>
                {
                    AsyncStart(serial, dnsServers, routes, port);
                }
            });
            adbMonitor.Monitor();
        }


        private static void CmdStop(string serial)
        {
            Log.I(TAG, "Stopping client...");
            ExecAdb(serial, "shell", "am", "start", "-a", "com.genymobile.gnirehtet.STOP", "-n", "com.genymobile.gnirehtet/.GnirehtetActivity");
        }

        private static void CmdRestart(string serial, string dnsServers, string routes, int port)
        {
            CmdStop(serial);
            CmdStart(serial, dnsServers, routes, port);
        }

        private static void CmdTunnel(string serial, int port)
        {
            ExecAdb(serial, "reverse", "localabstract:gnirehtet", $"tcp:{port}");
        }

        // If Relay is a class in a different namespace, update the using directive accordingly.  
        // If Relay is not a class but a namespace, you need to reference the correct class within that namespace.

        private static void CmdRelay(int port)
        {
            Log.I(TAG, $"Starting relay server on port {port}...");
            var relayServer = new Genymobile.Gnirehtet.Relay.Relay(port); // Fully qualify the Relay class to avoid ambiguity
            relayServer.Run();
        }

        private static void AsyncStart(string serial, string dnsServers, string routes, int port)
        {
            new Thread(() =>
            {
                try
                {
                    CmdStart(serial, dnsServers, routes, port);
                }
                catch (Exception ex)
                {
                    Log.E(TAG, "Cannot start client", ex);
                }
            }).Start();
        }

        private static void ExecAdb(string serial, params string[] adbArgs)
        {
            ExecSync(CreateAdbCommand(serial, adbArgs));
        }

        private static List<string> CreateAdbCommand(string serial, params string[] adbArgs)
        {
            var command = new List<string> { GetAdbPath() };
            if (!string.IsNullOrEmpty(serial))
            {
                command.Add("-s");
                command.Add(serial);
            }
            command.AddRange(adbArgs);
            return command;
        }

        private static void ExecAdb(string serial, List<string> adbArgList)
        {
            ExecAdb(serial, adbArgList.ToArray());
        }

        private static void ExecSync(List<string> command)
        {
            Log.D(TAG, $"Execute: {string.Join(" ", command)}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command[0],
                    Arguments = string.Join(" ", command.Skip(1)),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new CommandExecutionException(command, process.ExitCode);
            }
        }

        private static bool MustInstallClient(string serial)
        {
            Log.I(TAG, "Checking gnirehtet client...");
            var command = CreateAdbCommand(serial, "shell", "dumpsys", "package", "com.genymobile.gnirehtet");
            Log.D(TAG, $"Execute: {string.Join(" ", command)}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command[0],
                    Arguments = string.Join(" ", command.Skip(1)),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new CommandExecutionException(command, process.ExitCode);
            }

            var pattern = new Regex(@"^    versionCode=(\d+).*", RegexOptions.Multiline);
            var match = pattern.Match(output);
            if (match.Success)
            {
                string installedVersionCode = match.Groups[1].Value;
                return installedVersionCode != REQUIRED_APK_VERSION_CODE;
            }
            return true;
        }

        private static void PrintUsage()
        {
            var builder = new StringBuilder("Syntax: gnirehtet (");
            var commands = Enum.GetValues(typeof(Command)).Cast<Command>().ToArray();
            for (int i = 0; i < commands.Length; i++)
            {
                if (i > 0) builder.Append("|");
                builder.Append(CommandDefinitions[commands[i]].Name);
            }
            builder.Append(") ...").Append(NL);

            foreach (var command in commands)
            {
                builder.Append(NL);
                AppendCommandUsage(builder, command);
            }

            Console.Error.Write(builder.ToString());
        }

        private static void AppendCommandUsage(StringBuilder builder, Command command)
        {
            var (name, getDescription, _) = CommandDefinitions[command];
            builder.Append($"  gnirehtet {name}");
            if (((int)command & CommandLineArguments.PARAM_SERIAL) != 0) builder.Append(" [serial]");
            if (((int)command & CommandLineArguments.PARAM_DNS_SERVER) != 0) builder.Append(" [-d DNS[,DNS2,...]]");
            if (((int)command & CommandLineArguments.PARAM_PORT) != 0) builder.Append(" [-p PORT]");
            if (((int)command & CommandLineArguments.PARAM_ROUTES) != 0) builder.Append(" [-r ROUTE[,ROUTE2,...]]");
            builder.Append(NL);
            foreach (var descLine in getDescription().Split('\n'))
            {
                builder.Append($"      {descLine}").Append(NL);
            }
        }

        private static void PrintCommandUsage(Command command)
        {
            var builder = new StringBuilder();
            AppendCommandUsage(builder, command);
            Console.Error.Write(builder.ToString());
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string cmd = args[0];
            foreach (var command in CommandDefinitions)
            {
                if (cmd.Equals(command.Value.Name))
                {
                    var commandArgs = args.Skip(1).ToArray();
                    CommandLineArguments arguments;
                    try
                    {
                        arguments = CommandLineArguments.Parse((int)command.Key, commandArgs);
                    }
                    catch (ArgumentException ex)
                    {
                        Log.E(TAG, ex.Message);
                        PrintCommandUsage(command.Key);
                        return;
                    }

                    try
                    {
                        command.Value.Execute(arguments);
                    }
                    catch (Exception ex)
                    {
                        Log.E(TAG, $"Command '{cmd}' failed", ex);
                    }
                    return;
                }
            }

            if ("rt".Equals(cmd))
            {
                Log.E(TAG, "The 'rt' command has been renamed to 'run'. Try 'gnirehtet run' instead.");
                PrintCommandUsage(Command.RUN);
            }
            else
            {
                Log.E(TAG, $"Unknown command: {cmd}");
                PrintUsage();
            }
        }
    }
}