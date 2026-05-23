using System;
using System.Collections.Generic;
using SnakeOJTester;

internal static class SmokeTest
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("usage: SmokeTest.exe smoke_student.exe");
            return 2;
        }

        Direction parsedDirection;
        if (DirectionHelper.TryParse("w", out parsedDirection))
        {
            Console.WriteLine("lowercase-direction-accepted");
            return 1;
        }

        List<TestCase> cases = TestCaseFactory.CreateDefaultCases();
        RunOptions options = new RunOptions();
        options.LineTimeoutMs = 1000;
        options.FinalTimeoutMs = 1500;
        JudgeResult result = JudgeEngine.RunCase(args[0], cases[0], options);
        Console.WriteLine(result.StatusText);
        Console.WriteLine(result.Message);
        Console.WriteLine("score=" + result.Score + " steps=" + result.Steps + " snapshots=" + result.Snapshots.Count);
        return result.Status == JudgeStatus.Accepted ? 0 : 1;
    }
}
