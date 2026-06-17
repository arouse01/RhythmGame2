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
using UnityEngine.Audio;

public enum EventType
{
    Game,  // information about the game itself (version, app name, which game)
    Session,  // Session information like animal, 
    TrialParam,  // trial-specific parameters from the parameter file
    Feedback,  // LRS feedback information
    Beat,  // beat timing information
    Response  // user-initiated event information
}

public struct Entry
{
    public double RawDSPTime;  // raw dspTime, not referenced to session or trial start
    public double? ScheduledTime;

    public EventType Type;

    public int? TrialIndex;

    public int? Lane;
    public int? BeatIndex;
    public double? Phase;

    public string EventMessage;
    public string EventValue;

    public static Entry Game(double rawDspTime, string eventMessage, string eventValue)
    {
        return new Entry
        {
            RawDSPTime = rawDspTime,
            Type = EventType.Game,
            EventMessage = eventMessage,
            EventValue = eventValue
        };
    }

    public static Entry Session(double rawDspTime, string eventMessage, string eventValue)
    {
        return new Entry
        {
            RawDSPTime = rawDspTime,
            Type = EventType.Session,
            EventMessage = eventMessage,
            EventValue = eventValue
        };
    }

    public static Entry Trial(double rawDspTime, int trialIndex, string message, string value = "")
    {
        return new Entry
        {
            Type = EventType.TrialParam,
            RawDSPTime = rawDspTime,
            TrialIndex = trialIndex,
            EventMessage = message,
            EventValue = value
        };
    }

    public static Entry Response(double rawDspTime, int trialIndex, int lane, string message, double phase)
    {
        
        return new Entry
        {
            Type = EventType.Response,
            RawDSPTime = rawDspTime,
            TrialIndex = trialIndex,
            Lane = lane,
            EventMessage = message,
            Phase = phase,
        };
    }

    public static Entry Beat(double rawDspTime, double scheduledTime, int trialIndex, int beatIndex, int lane, string message)
    {

        return new Entry
        {
            Type = EventType.Beat,
            RawDSPTime = rawDspTime,
            ScheduledTime = scheduledTime,
            TrialIndex = trialIndex,
            BeatIndex = beatIndex,
            Lane = lane,
            EventMessage = message
        };
    }

    public static Entry Feedback(double rawDspTime, int trialIndex, string message)
    {

        return new Entry
        {
            Type = EventType.Feedback,
            RawDSPTime = rawDspTime,
            TrialIndex = trialIndex,
            EventMessage = message
        };
    }

}

public static class Logger
{
    // central handler for logging session events

    private static string logFilePath = Application.dataPath + "/EventLog.txt";
    private static ConcurrentQueue<string> logQueue = new();
    //private static bool isLogging = false;
    private static readonly CancellationTokenSource cts = new();
    private static Task logTask;

    private static double sessionStartDspTime;
    private static double trialStartDspTime;
    private static double sessionStartReal;  // time from Time.realtimeSinceStartup

    public static void SetLogFilePath(string path)
    {
        logFilePath = path;
    }

    public static void StartSession(double startTime)
    {
        sessionStartDspTime = startTime;
        sessionStartReal = Time.realtimeSinceStartup;
    }

    public static void StartTrial(double startTime)
    {
        trialStartDspTime = startTime;
    }

    public static void LogData(string eventType, string eventMessage, string eventValue=null)
    {
        //string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        double time = TimeUtil.fixedTimeAsDouble;  // fixedTimeAsDouble uses the physics timing rather than the frame timing
        //double time = AudioSettings.dspTime;  // fixedTimeAsDouble uses the physics timing rather than the frame timing
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

    
    public static void Log(Entry log)
    {
        //string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        double sessionTime = log.RawDSPTime - sessionStartDspTime;
        double trialTime;
        if (log.Type == EventType.Game || log.Type == EventType.Session)
        {
            trialTime = 0;  // Game data and session data don't have a specific trial time because they happen outside of trials
        }
        else
        {
            trialTime = log.RawDSPTime - trialStartDspTime;
        }
        double realTime = Time.realtimeSinceStartup - sessionStartReal;
        string line = 
            $"{realTime}\t" +
            $"{log.Type}\t" +
            $"{sessionTime}\t" +
            $"{log.TrialIndex}\t" +
            $"{trialTime}\t" +
            $"{log.Lane}\t" +
            $"{log.BeatIndex}\t" +
            $"{log.ScheduledTime}\t" +
            $"{log.Phase}\t" +
            $"{log.EventMessage}\t" +
            $"{log.EventValue}";
        
        logQueue.Enqueue(line);

    }

    public static void LogEvent(double eventTime, string eventType, string eventMessage, string eventValue = null)
    {
        //string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        double time = TimeUtil.fixedTimeAsDouble;  // fixedTimeAsDouble uses the physics timing rather than the frame timing
        //double time = AudioSettings.dspTime;  // fixedTimeAsDouble uses the physics timing rather than the frame timing

        logQueue.Enqueue($"{time}\t{eventTime}\t{eventType}\t{eventMessage}\t{eventValue}");

    }
    
    public static void StartLog()
    {
        logTask = Task.Run(ProcessQueue, cts.Token);

        string line =
            "RealTime\t" +
            "EventType\t" +
            "SessionTime\t" +
            "TrialIndex\t" +
            "TrialTime\t" +
            "Lane\t" +
            "BeatIndex\t" +
            "ScheduledTime\t" +
            "Phase\t" +
            "EventMessage\t" +
            "EventValue";

        logQueue.Enqueue(line);
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
