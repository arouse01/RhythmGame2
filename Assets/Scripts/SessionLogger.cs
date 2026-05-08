//using System.Collections;
//using System.Collections.Generic;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using TimeUtil = UnityEngine.Time;

public class EventLogger
{
    // central handler for logging session events

    private static string logFilePath = Application.dataPath + "/EventLog.txt";
    private static ConcurrentQueue<string> logQueue = new();
    //private static bool isLogging = false;
    private static readonly CancellationTokenSource cts = new();
    private static Task logTask;

    public static void SetLogFilePath(string path)
    {
        logFilePath = path;
    }

    public static void LogEvent(string eventType, string eventMessage, string eventValue=null)
    {
        //string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        double time = TimeUtil.fixedTimeAsDouble;  // fixedTimeAsDouble uses the physics timing rather than the frame timing
        //var t = ReliableTime.Time;
        //var sec = t % 60f;
        //var min = Math.Floor(t) / 60f % 60f;
        //var hrs = Math.Floor(t) / 3600f % 24f;
        
        //using (StreamWriter writer = new StreamWriter(logFilePath, true))
        //{
        //    writer.WriteLine($"{time}\t{eventType}\t{eventMessage}\t{eventValue}");
        //}

        logQueue.Enqueue($"{time}\t{eventType}\t{eventMessage}\t{eventValue}");
        //if (!isLogging)
        //{
        //    Task.Run(ProcessQueue);
        //}
    }

    public static void StartLog()
    {
        logTask = Task.Run(ProcessQueue, cts.Token);
    }

    public static void StopLog()
    {
        cts.Cancel();  // Signal the background task to stop
        try
        {
            logTask?.Wait();  // Ensure it finishes before exiting
        }
        catch (TaskCanceledException)
        {
            // expected during shutdown
        }
        catch (AggregateException ex) when (
            ex.InnerExceptions.All(e => e is TaskCanceledException))
        {
            // Expected during shutdown
        }

    }

    private static async Task ProcessQueue()
    {
        //isLogging = true;

        try
        {
            while (true)
            {
                while (logQueue.TryDequeue(out string logEntry))
                {
                    await File.AppendAllTextAsync(logFilePath, logEntry + "\n");
                    //await Task.Delay(5); // Prevents overwhelming file I/O
                }

                //isLogging = false;
                await Task.Delay(10, cts.Token);  // added cts.Token so it will quit if receiving the cancel signal, so added the try() and final log writing bit below
            }
        }
        catch (TaskCanceledException)
        {
            // Expected on shutdown
        }

        // Final flush after cancellation
        while (logQueue.TryDequeue(out string logEntry))
        {
            await File.AppendAllTextAsync(logFilePath, logEntry + "\n");
        }
    }
}
