using System;

namespace Gnirehtet
{
    public class CommandLineArguments
    {
        public const int PARAM_NONE = 0;
        public const int PARAM_SERIAL = 1;
        public const int PARAM_DNS_SERVER = 1 << 1;
        public const int PARAM_ROUTES = 1 << 2;
        public const int PARAM_PORT = 1 << 3;

        public const int DEFAULT_PORT = 31416;

        private int port;
        private string serial;
        private string dnsServers;
        private string routes;

        public static CommandLineArguments Parse(int acceptedParameters, params string[] args)
        {
            var arguments = new CommandLineArguments();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if ((acceptedParameters & PARAM_DNS_SERVER) != 0 && arg == "-d")
                {
                    if (arguments.dnsServers != null)
                    {
                        throw new ArgumentException("DNS servers already set");
                    }
                    if (i == args.Length - 1)
                    {
                        throw new ArgumentException("Missing -d parameter");
                    }
                    arguments.dnsServers = args[i + 1];
                    i++; // consume the -d parameter
                }
                else if ((acceptedParameters & PARAM_ROUTES) != 0 && arg == "-r")
                {
                    if (arguments.routes != null)
                    {
                        throw new ArgumentException("Routes already set");
                    }
                    if (i == args.Length - 1)
                    {
                        throw new ArgumentException("Missing -r parameter");
                    }
                    arguments.routes = args[i + 1];
                    i++; // consume the -r parameter
                }
                else if ((acceptedParameters & PARAM_PORT) != 0 && arg == "-p")
                {
                    if (arguments.port != 0)
                    {
                        throw new ArgumentException("Port already set");
                    }
                    if (i == args.Length - 1)
                    {
                        throw new ArgumentException("Missing -p parameter");
                    }
                    arguments.port = int.Parse(args[i + 1]);
                    if (arguments.port <= 0 || arguments.port >= 65536)
                    {
                        throw new ArgumentException($"Invalid port: {arguments.port}");
                    }
                    i++;
                }
                else if ((acceptedParameters & PARAM_SERIAL) != 0 && arguments.serial == null)
                {
                    arguments.serial = arg;
                }
                else
                {
                    throw new ArgumentException($"Unexpected argument: \"{arg}\"");
                }
            }
            if (arguments.port == 0)
            {
                arguments.port = DEFAULT_PORT;
            }
            return arguments;
        }

        public string Serial => serial;
        public string DnsServers => dnsServers;
        public string Routes => routes;
        public int Port => port;
    }
}