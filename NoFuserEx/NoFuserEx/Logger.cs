using System;
using System.Linq;

namespace NoFuserEx {
    internal class Logger {
        internal static void Information() {
            Console.Title = "NoFuserEx v1.1";
            Console.ForegroundColor = ConsoleColor.Red;
            const string text = "_____   __     __________                         __________       ";
            const string text1 = "___  | / /________  ____/___  _______________________  ____/___  __ ";
            const string text2 = @"__   |/ /_  __ \_  /_   _  / / /_  ___/  _ \_  ___/_  __/  __  |/_/";
            const string text3 = "_  /|  / / /_/ /  __/   / /_/ /_(__  )/  __/  /   _  /___  __>  <   ";
            const string text4 = @"/_/ |_/  \____//_/      \__,_/ /____/ \___//_/    /_____/  /_/|_|   ";
            const string text5 = "[ NoFuserEx - v1.1 ]";
            const string text6 = "- By CodeShark -";
            CenterWrite(text);
            CenterWrite(text1);
            CenterWrite(text2);
            CenterWrite(text3);
            CenterWrite(text4);
            CenterWrite(text5);
            CenterWrite(text6);
            WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.White;
        }

        internal static void Help() {
            WriteLine(@"
For deobfuscate an assembly protected with any version of ConfuserEx (and some modded too) you just have to drag and drop the file in this .exe.

Some options that you can use:
* --force-deob for force the deobfuscation.
* --dont-unpack for don't unpack the assembly.
* --dont-tamper for don't remove anti-tamper.
* --dont-constants for don't decrypt the constants.
* --dont-cflow for don't deobfuscate the control flow.
* --dont-proxy-calls for don't replace the proxy calls.
* --dont-remove-junk-methods for don't remove the junk methods.
* --dont-resources for don't decrypt the protected resources.
* --dont-rename for don't rename the obfuscated names.
* -v for verbose information.
* -vv for very verbose information.

If something doesn't work properly, you can notify me or fix it yourself.
This is an open-source, if you paid for this, you got ripped off :P");
            WriteLine(string.Empty);
        }

        internal static void CenterWrite(string text) {
            Write(new string(' ', (Console.WindowWidth - text.Length) / 2));
            WriteLine(text);
        }

        internal static void Info(string text) {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Write("[~] ");
            Console.ForegroundColor = ConsoleColor.White;
            WriteLine(text);
        }

        internal static void Error(string text) {
            Console.ForegroundColor = ConsoleColor.Red;
            Write("        [-] ");
            Console.ForegroundColor = ConsoleColor.White;
            WriteLine(text);
        }

        internal static void Exclamation(string text) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Write("        [!] ");
            Console.ForegroundColor = ConsoleColor.White;
            WriteLine(text);
        }

        internal static void Success(string message) {
            Console.ForegroundColor = ConsoleColor.Green;
            Write("        [+] ");
            Console.ForegroundColor = ConsoleColor.White;
            WriteLine(message);
        }

        internal static void Exception(Exception exception) {
            WriteLine(string.Empty);
            WriteLine($"Error message: {exception.Message}");
            if (Options.Verbose)
                WriteLine($"Error stack trace: {exception.StackTrace}");
            WriteLine(string.Empty);
            throw exception;
        }

        internal static void Verbose(string text) {
            if (Options.Verbose)
                WriteLine(text);
        }

        internal static void VeryVerbose(string text) {
            if (Options.VeryVerbose)
                Verbose(text);
        }

        internal static void Write(string text) {
            Console.Write(text);
        }

        internal static void WriteLine(string text) {
            Console.WriteLine(text);
        }

        internal static void Exit(bool info = true) {
            const string exitMessage = "Press any key to exit...";
            if (IsN00bUser()) {
                if (info)
                    Info(exitMessage);
                else
                    WriteLine(exitMessage);
                Console.ReadKey(true);
            }
            Environment.Exit(0);
        }

        // Thanks to 0xd4d
        static bool IsN00bUser() {
            if (HasEnv("VisualStudioDir"))
                return false;
            if (HasEnv("SHELL"))
                return false;
            return HasEnv("windir") && !HasEnv("PROMPT");
        }

        static bool HasEnv(string name) {
            return
                Environment.GetEnvironmentVariables()
                    .Keys.OfType<string>()
                    .Any(env => string.Equals(env, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
