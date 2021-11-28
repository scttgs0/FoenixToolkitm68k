using System;
using System.Collections.Generic;

using Gtk;


namespace FoenixToolkit.UI
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Dictionary<string, string> context = null;
            bool OkToContinue = true;

            if (args.Length > 0)
            {
                Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
                context = DecodeProgramArguments(args);
                OkToContinue = "true".Equals(context["Continue"]);
            }

            if (OkToContinue)
            {
                Application.Init();

                var app = new Application("org.FoenixToolkit.FoenixToolkit", GLib.ApplicationFlags.None);
                app.Register(GLib.Cancellable.Current);

                var win = new MainWindow(context);
                app.AddWindow(win);

                win.Show();
                Application.Run();
            }
        }

        private static Dictionary<String,String> DecodeProgramArguments(string[] args)
        {
            Dictionary<string, string> context = new()
            {
                { "Continue", "true" }
            };

            for (int i = 0; i < args.Length; ++i)
            {
                switch (args[i].Trim())
                {
                    // the hex file to load is specified
                    case "-k":
                    case "--kernel":
                        // a kernel file must be specified
                        if (args.Length == i + 1 || args[i + 1].Trim().StartsWith("-") || !args[i + 1].Trim().EndsWith("kernel"))
                        {
                            Console.Out.WriteLine("You must specify a kernel file.");
                            context["Continue"] = "false";
                            break;
                        }

                        context.Add("defaultKernel", args[i + 1]);
                        i++; // skip the next argument
                        break;

                    case "-j":
                    case "--jump":
                        // An address must be specified
                        if (args.Length == i + 1 || args[i + 1].Trim().StartsWith("-")) {
                            Console.Out.WriteLine("You must specify a jump address.");
                            context["Continue"] = "false";
                            break;
                        }

                        int value = -1;
                        try {
                            value = Convert.ToInt32(args[i + 1].Replace("$:", ""), 16);
                        }
                        catch (System.FormatException) {}

                        if (value > 0)
                        {
                            context.Add("jumpStartAddress", value.ToString());
                            i++; // skip the next argument
                        }
                        else
                        {
                            Console.Out.WriteLine("Invalid address specified: " + args[i + 1]);
                            context["Continue"] = "false";
                        }
                        break;

                    // Autorun - a value is not expected for this one
                    case "-r":
                    case "--run":
                        string runValue = "true";

                        if (args.Length > (i + 1) && !args[i + 1].Trim().StartsWith("-"))
                        {
                            runValue = args[i + 1];

                            if (!"true".Equals(runValue) && !"false".Equals(runValue))
                            {
                                runValue = "true";
                            }

                            i++; // skip the next argument
                        }

                        context.Add("autoRun", runValue);
                        break;

                    // Disable IRQs - a value is not expected for this one
                    case "-i":
                    case "--irq":
                        context.Add("disabledIRQs", "true");
                        break;

                    // Board Version U or K
                    case "-b":
                    case "--board":
                        if (args.Length == i + 1 || args[i + 1].Trim().StartsWith("-")) {
                            Console.Out.WriteLine("You must specify a board version.");
                            context["Continue"] = "false";
                            break;
                        }

                        string verArg = args[i + 1];
                        ++i; // skip the next argument

                        switch (verArg.ToLower())
                        {
                            case "k":
                                context.Add("version", "A2560K");
                                break;

                            case "u":
                                context.Add("version", "A2560U");
                                break;
                        }
                        break;

                    case "--help":
                        DisplayUsage();

                        context["Continue"] = "false";
                        break;

                    default:
                        Console.Out.WriteLine("Unknown switch used:" + args[i].Trim());

                        DisplayUsage();

                        context["Continue"] = "false";
                        break;
                }
            }

            return context;
        }

        static void DisplayUsage()
        {
            Console.Out.WriteLine("Foenix Toolkit Usage:");
            Console.Out.WriteLine("   -k, --kernel: kernel file name (srec)");
            Console.Out.WriteLine("   -j, --jump: jump to specified address");
            Console.Out.WriteLine("   -r, --run: autorun true/false");
            Console.Out.WriteLine("   -i, --irq: disable IRQs true/false");
            Console.Out.WriteLine("   -b, --board: board version (u or k)");
            Console.Out.WriteLine("   --help: show this usage");
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Application.Quit();
        }
    }
}
