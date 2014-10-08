﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvroTests
{
    using System.Globalization;
    using System.Reactive.Disposables;
    using System.Reflection;
    using System.Threading;

    public static class ConsoleHost
    {
        public static void WithOptions(Dictionary<string, Func<CancellationToken, Task>> actions)
        {
            while (true)
            {
                var tokenSource = new CancellationTokenSource();

                using (Color(ConsoleColor.Yellow))
                {
                    Console.WriteLine();
                    Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name);
                    Console.WriteLine();
                }

                actions.Keys.Select((title, index) => new { title, index })
                    .ToList()
                    .ForEach(t => Console.WriteLine("[{0}] {1}", t.index + 1, t.title));

                Console.WriteLine();
                Console.Write("Select an option: ");

                var key = Console.ReadKey().KeyChar.ToString(CultureInfo.InvariantCulture);
                Console.WriteLine();

                int option = 1;
                if (!int.TryParse(key, out option))
                {
                    Environment.Exit(0);
                }

                option--;

                var selection = actions.ToList()[option];

                Console.Write("executing ");
                using (Color(ConsoleColor.Green))
                {
                    Console.WriteLine(selection.Key);
                }

                var running = selection
                    .Value(tokenSource.Token)
                    .ContinueWith(ReportTaskStatus);

                using (Color(ConsoleColor.DarkGreen))
                {
                    Console.WriteLine("press `q` to signal termination");
                }

                var input = Console.ReadKey();
                if (input.KeyChar == 'q')
                {
                    using (Color(ConsoleColor.DarkGreen))
                    {
                        Console.WriteLine();
                        Console.WriteLine("termination signal sent");
                    }
                    tokenSource.Cancel();
                }

                running.Wait();

                Console.ReadKey();
                Console.Clear();
            }
        }

        private static void ReportTaskStatus(Task task)
        {
            if (task.IsFaulted)
            {
                using (Color(ConsoleColor.Red))
                {
                    Console.WriteLine("an exception occurred");
                }
                Console.WriteLine(task.Exception);
            }
            else if (task.IsCanceled)
            {
                using (Color(ConsoleColor.DarkYellow))
                {
                    Console.WriteLine("cancelled");
                }
            }
            else
            {
                using (Color(ConsoleColor.Blue))
                {
                    Console.WriteLine("completed successfully");
                }
            }

            using (Color(ConsoleColor.DarkGreen))
            {
                Console.WriteLine("press any key to return to the menu");
            }
        }

        public static IDisposable Color(ConsoleColor color)
        {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = color;

            return Disposable.Create(
                () => Console.ForegroundColor = original
                );
        }
    }
}
