using System;

namespace userScript
{
    public static class Log
    {
        public static void Err(object msg)
        {
            ConsoleWriteColor("Error: " + msg, ConsoleColor.Red);
        }

        public static void Warn(object msg)
        {
            ConsoleWriteColor("Warn:  " + msg, ConsoleColor.Yellow);
        }

        public static void Info(object msg)
        {
            ConsoleWriteColor("Info:  " + msg, Console.ForegroundColor);
        }

        public static void Ok(object msg)
        {
            ConsoleWriteColor("Success:  " + msg, ConsoleColor.DarkGreen);
        }

        private static void ConsoleWriteColor(string msg, ConsoleColor color)
        {
            var conColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(msg + Environment.NewLine);
            Console.ForegroundColor = conColor;
        }
    }
}