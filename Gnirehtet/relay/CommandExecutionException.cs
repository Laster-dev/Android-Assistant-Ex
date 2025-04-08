using System;
using System.Collections.Generic;

namespace Gnirehtet.Relay
{
    public class CommandExecutionException : Exception
    {
        private readonly List<string> command;
        private readonly int exitCode;

        public CommandExecutionException(List<string> command, int exitCode)
            : base(CreateMessage(command, exitCode))
        {
            this.command = command;
            this.exitCode = exitCode;
        }

        private static string CreateMessage(List<string> command, int exitCode)
        {
            return $"Command {string.Join(" ", command)} returned with value {exitCode}";
        }

        public int ExitCode => exitCode;

        public List<string> Command => command;
    }
}