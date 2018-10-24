using System;
using Liviano;
using CommandLine;

namespace Liviano.CLI
{
    class Program
    {
        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('h', "help", Required = false, HelpText = "See help")]
            public bool Help { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                if (o.Verbose)
                {
                    Console.WriteLine($"Verbose output enabled. Current Arguments: -v {o.Verbose}");
                    Console.WriteLine("Quick Start Example! App is in Verbose mode!");
                }
                else if (o.Help)
                {
                    Console.WriteLine($"Help! current argument: -h {o.Help}");
                }
                else
                {
                    Console.WriteLine($"Current Arguments: -h {o.Help}");
                    Console.WriteLine("Help!");
                }
            });
        }
    }
}
