using System;
using SnakeOJTester;

internal static class CompileLimitHarness
{
    private static int Main()
    {
        RunOptions options = new RunOptions();
        string code = "int main(){return 0;}\n" + new string(' ', 33 * 1024);
        CompileResult result = JudgeEngine.Compile(code, "limit_work", options);
        Console.WriteLine(result.Success ? "unexpected-success" : result.Message);
        return result.Success ? 1 : 0;
    }
}
