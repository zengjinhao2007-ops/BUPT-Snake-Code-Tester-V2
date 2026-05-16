using System;
using System.Collections.Generic;
using SnakeOJTester;

internal static class ScoreHarness
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("usage: ScoreHarness.exe student.exe");
            return 2;
        }

        RunOptions options = new RunOptions();
        options.LineTimeoutMs = 1000;
        options.FinalTimeoutMs = 1500;
        options.CaptureDetails = false;
        options.MaxSnapshotsToKeep = 0;
        options.MaxLogChars = 0;
        List<TestCase> cases = TestCaseFactory.CreateDefaultCases();
        List<TestCase> scoringCases = new List<TestCase>();
        for (int i = 0; i < cases.Count; i++)
        {
            if (cases[i].IsScoringCase) scoringCases.Add(cases[i]);
        }

        JudgeResult[] results = new JudgeResult[scoringCases.Count];
        for (int i = 0; i < scoringCases.Count; i++)
        {
            results[i] = JudgeEngine.RunCase(args[0], scoringCases[i], options);
        }

        double weighted = 0.0;
        int raw = 0;
        int count = 0;
        for (int i = 0; i < scoringCases.Count; i++)
        {
            JudgeResult result = results[i];
            double w = result.N <= 0 ? 0.0 : 1.0 / ((Math.Log(result.N) / Math.Log(2.0)) + 1.0);
            weighted += result.Score * w;
            raw += result.Score;
            count++;
            string timeText = result.ElapsedMs.ToString();
            if (result.TimeLimitExceededAtMs > 0)
            {
                timeText += " timeoutAt=" + result.TimeLimitExceededAtMs;
                if (result.StoppedAtDiagnosticLimit)
                {
                    timeText += " diagnosticLimit=" + result.DiagnosticLimitMs;
                }
            }
            Console.WriteLine(scoringCases[i].Name + " status=" + result.StatusText + " score=" + result.Score + " weighted=" + (result.Score * w).ToString("0.##") + " timeMs=" + timeText);
        }
        Console.WriteLine("count=" + count + " raw=" + raw + " weighted=" + weighted.ToString("0.##"));
        return 0;
    }
}
