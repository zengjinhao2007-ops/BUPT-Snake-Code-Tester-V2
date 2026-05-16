using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;

namespace SnakeOJTester
{
    public static class JudgeEngine
    {
        public static string FindGcc()
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            string[] parts = path.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string dir = parts[i].Trim();
                if (dir.Length == 0)
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(dir, "gcc.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
            return "";
        }

        public static string GetGccVersion(string gccPath)
        {
            if (string.IsNullOrEmpty(gccPath) || !File.Exists(gccPath))
            {
                return "未检测到 gcc。请安装 MinGW-w64 或 MSYS2，并把 gcc.exe 加入 PATH。";
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = gccPath;
                psi.Arguments = "--version";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string firstLine = p.StandardOutput.ReadLine();
                    p.WaitForExit(2000);
                    if (!string.IsNullOrEmpty(firstLine))
                    {
                        return firstLine + "  (" + gccPath + ")";
                    }
                }
            }
            catch (Exception ex)
            {
                return "gcc 检测失败：" + ex.Message;
            }
            return "已检测到 gcc：" + gccPath;
        }

        public static CompileResult Compile(string code, string workDir, RunOptions options)
        {
            CompileResult result = new CompileResult();
            string normalizedCode = code == null ? "" : code;
            result.SourceFingerprint = SourceFingerprint(normalizedCode);
            int codeBytes = Encoding.UTF8.GetByteCount(normalizedCode);
            int codeLimitBytes = options.CodeLengthLimitKb * 1024;
            if (codeLimitBytes > 0 && codeBytes > codeLimitBytes)
            {
                result.Success = false;
                result.Message = "代码长度超过限制：当前 " + codeBytes + " 字节，限制 " + codeLimitBytes + " 字节（" + options.CodeLengthLimitKb + "KB）。";
                result.CompilerOutput = "请删减调试输出、无用注释或过长的内置数据。";
                return result;
            }

            string gccPath = FindGcc();
            if (string.IsNullOrEmpty(gccPath))
            {
                result.Success = false;
                result.Message = "未检测到 gcc。请安装 MinGW-w64 或 MSYS2，并把 gcc.exe 加入 PATH 后重试。";
                return result;
            }

            try
            {
                Directory.CreateDirectory(workDir);
                string runDir = Path.Combine(workDir, "run_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(runDir);
                string sourcePath = Path.Combine(runDir, "student.c");
                string exePath = Path.Combine(runDir, "student.exe");
                result.SourcePath = sourcePath;
                result.ExePath = exePath;
                result.SourceFingerprint = SourceFingerprint(normalizedCode);
                File.WriteAllText(sourcePath, normalizedCode, new UTF8Encoding(false));

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = gccPath;
                long stackBytes = Math.Max(0, options.StackLimitKb) * 1024L;
                string stackArg = stackBytes > 0 ? " -Wl,--stack," + stackBytes : "";
                psi.Arguments = "-std=c11 -O2 -Wall -Wextra" + stackArg + " -o " + Quote(exePath) + " " + Quote(sourcePath);
                psi.WorkingDirectory = runDir;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                StringBuilder output = new StringBuilder();
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) output.AppendLine(e.Data);
                    };
                    p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) output.AppendLine(e.Data);
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    bool exited = p.WaitForExit(options.CompileTimeoutMs);
                    if (!exited)
                    {
                        TryKill(p);
                        result.Success = false;
                        result.Message = "编译超时。请检查代码中是否触发了编译器异常或文件被占用。";
                        result.CompilerOutput = output.ToString();
                        return result;
                    }
                    p.WaitForExit();
                    result.CompilerOutput = output.ToString();
                    if (p.ExitCode != 0 || !File.Exists(exePath))
                    {
                        result.Success = false;
                        result.Message = "编译失败，请查看下方 gcc 报错信息。";
                        return result;
                    }

                    result.Success = true;
                    result.Message = "编译成功：" + exePath + Environment.NewLine + "源码指纹：" + result.SourceFingerprint + Environment.NewLine + "源码文件：" + sourcePath;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "编译过程异常：" + ex.Message;
                return result;
            }
        }

        public static JudgeResult RunCase(string exePath, TestCase testCase, RunOptions options)
        {
            JudgeResult result = new JudgeResult();
            result.CaseName = testCase.Name;
            result.N = testCase.N;
            Stopwatch elapsed = Stopwatch.StartNew();

            SnakeState state;
            try
            {
                state = SnakeState.FromTestCase(testCase);
            }
            catch (Exception ex)
            {
                SetStatus(result, JudgeStatus.RuntimeError, "测试用例内部错误：" + ex.Message);
                return result;
            }

            CStyleRandom random = new CStyleRandom(testCase.Seed);
            LimitedRunRecorder recorder = new LimitedRunRecorder(options.CaptureDetails, options.MaxSnapshotsToKeep, options.MaxLogChars, options.SnapshotInterval);

            StudentSession session = new StudentSession();
            try
            {
                session.Start(exePath, Path.GetDirectoryName(exePath), options);
                string initialInput = testCase.GetInitialInput();
                session.Write(initialInput);
                recorder.AppendLine("OJ -> 程序：初始地图与 N");
                recorder.AppendLine(initialInput.TrimEnd());
                recorder.AppendLine("");

                if (recorder.ShouldCaptureSnapshot(state.MoveCount, true))
                {
                    recorder.AddSnapshot(state.CreateSnapshot(testCase.Name, "", "", false, false, false, "", "初始状态"));
                }

                while (true)
                {
                    string moveLine;
                    if (!session.TryReadLine(options.LineTimeoutMs, out moveLine))
                    {
                        string extra = session.GetStderr();
                        if (session.LimitStatus != JudgeStatus.NotRun)
                        {
                            SetStatus(result, session.LimitStatus, session.LimitMessage);
                        }
                        else if (session.HasExited && session.ExitCode != 0)
                        {
                            SetStatus(result, JudgeStatus.RuntimeError, "程序提前异常退出，退出码：" + session.ExitCode + "。");
                        }
                        else
                        {
                            SetStatus(result, JudgeStatus.TimeLimitExceeded, "等待方向输出超时。常见原因：忘记 fflush(stdout)、程序在读入前卡住、或进入死循环。");
                        }
                        result.ProgramError = extra;
                        result.Score = state.Score;
                        result.Steps = state.MoveCount;
                        break;
                    }

                    string scoreLine;
                    if (!session.TryReadLine(options.LineTimeoutMs, out scoreLine))
                    {
                        if (session.LimitStatus != JudgeStatus.NotRun)
                        {
                            SetStatus(result, session.LimitStatus, session.LimitMessage);
                        }
                        else if (session.HasExited && session.ExitCode != 0)
                        {
                            SetStatus(result, JudgeStatus.RuntimeError, "程序输出方向 `" + moveLine + "` 后提前异常退出，退出码：" + session.ExitCode + "。");
                        }
                        else if (session.HasExited && session.ExitCode == 0)
                        {
                            SetStatus(result, JudgeStatus.PresentationError, "程序输出方向 `" + moveLine + "` 后正常退出，但没有输出第二行分数。");
                        }
                        else
                        {
                            SetStatus(result, JudgeStatus.TimeLimitExceeded, "读取到方向 `" + moveLine + "`，但等待分数输出超时。请在输出方向和分数后都刷新 stdout。");
                        }
                        result.ProgramError = session.GetStderr();
                        result.Score = state.Score;
                        result.Steps = state.MoveCount;
                        break;
                    }

                    recorder.AppendLine("程序 -> OJ：");
                    recorder.AppendLine(moveLine);
                    recorder.AppendLine(scoreLine);

                    Direction move;
                    if (!DirectionHelper.TryParse(moveLine, out move))
                    {
                        SetStatus(result, JudgeStatus.PresentationError, "方向输出格式错误：应只输出 W/A/S/D 之一，实际为 `" + moveLine + "`。");
                        result.Score = state.Score;
                        result.Steps = state.MoveCount;
                        break;
                    }

                    int reportedScore;
                    if (!int.TryParse(scoreLine.Trim(), out reportedScore))
                    {
                        SetStatus(result, JudgeStatus.PresentationError, "分数输出格式错误：第二行应为整数，实际为 `" + scoreLine + "`。");
                        result.Score = state.Score;
                        result.Steps = state.MoveCount;
                        break;
                    }

                    if (reportedScore != state.Score)
                    {
                        SetStatus(result, JudgeStatus.WrongAnswer, "分数不一致：本步移动前真实分数是 " + state.Score + "，程序输出了 " + reportedScore + "。");
                        if (recorder.ShouldCaptureSnapshot(state.MoveCount, true))
                        {
                            recorder.AddSnapshot(state.CreateSnapshot(testCase.Name, moveLine, "", false, false, false, "", "分数校验失败"));
                        }
                        result.Score = state.Score;
                        result.Steps = state.MoveCount;
                        break;
                    }

                    MoveOutcome outcome = state.ApplyMove(move, random);
                    if (outcome.Ended)
                    {
                        session.WriteLine("100 100");
                        recorder.AppendLine("OJ -> 程序：");
                        recorder.AppendLine("100 100");
                        recorder.AppendLine("");

                        if (recorder.ShouldCaptureSnapshot(state.MoveCount, true))
                        {
                            recorder.AddSnapshot(state.CreateSnapshot(testCase.Name, moveLine, "100 100", outcome.AteFood, outcome.Grew, true, outcome.EndReason, "本条指令导致游戏结束，最终地图应输出碰撞前一刻。"));
                        }
                        FinishAfterGameOver(result, session, state, options, recorder);
                        break;
                    }

                    session.WriteLine(outcome.Response);
                    recorder.AppendLine("OJ -> 程序：");
                    recorder.AppendLine(outcome.Response);
                    recorder.AppendLine("");
                    if (recorder.ShouldCaptureSnapshot(state.MoveCount, false))
                    {
                        recorder.AddSnapshot(state.CreateSnapshot(testCase.Name, moveLine, outcome.Response, outcome.AteFood, outcome.Grew, false, "", outcome.Note));
                    }
                }
            }
            catch (Exception ex)
            {
                if (session.LimitStatus != JudgeStatus.NotRun)
                {
                    SetStatus(result, session.LimitStatus, session.LimitMessage);
                }
                else
                {
                    SetStatus(result, JudgeStatus.RuntimeError, "评测过程异常：" + ex.Message);
                }
                result.Score = state.Score;
                result.Steps = state.MoveCount;
            }
            finally
            {
                elapsed.Stop();
                long programElapsedMs = session.ProgramElapsedMs;
                long timeLimitExceededAtMs = session.TimeLimitExceededAtMs;
                result.TimeLimitExceededAtMs = timeLimitExceededAtMs;
                result.DiagnosticLimitMs = session.DiagnosticLimitMs;
                result.StoppedAtDiagnosticLimit = session.StoppedAtDiagnosticLimit;
                if (!result.StoppedAtDiagnosticLimit
                    && timeLimitExceededAtMs > 0
                    && result.DiagnosticLimitMs > 0
                    && programElapsedMs >= result.DiagnosticLimitMs - 5
                    && !session.HasExited)
                {
                    result.StoppedAtDiagnosticLimit = true;
                }
                if (session.LimitStatus != JudgeStatus.NotRun)
                {
                    SetStatus(result, session.LimitStatus, session.LimitMessage);
                }
                else if (result.StoppedAtDiagnosticLimit && timeLimitExceededAtMs > 0)
                {
                    SetStatus(result, JudgeStatus.TimeLimitExceeded, TimeLimitMessage(options, timeLimitExceededAtMs, programElapsedMs, true));
                }
                else if (timeLimitExceededAtMs > 0 && result.Status != JudgeStatus.MemoryLimitExceeded)
                {
                    SetStatus(result, JudgeStatus.TimeLimitExceeded, TimeLimitMessage(options, timeLimitExceededAtMs, programElapsedMs, false));
                }
                else if (options.TimeLimitMs > 0
                    && programElapsedMs > options.TimeLimitMs
                    && result.Status != JudgeStatus.MemoryLimitExceeded)
                {
                    timeLimitExceededAtMs = programElapsedMs;
                    result.TimeLimitExceededAtMs = timeLimitExceededAtMs;
                    SetStatus(result, JudgeStatus.TimeLimitExceeded, TimeLimitMessage(options, timeLimitExceededAtMs, programElapsedMs, false));
                }
                result.ProgramElapsedMs = programElapsedMs;
                result.ElapsedMs = programElapsedMs;
                result.InteractionLog = recorder.GetLog();
                result.Snapshots = recorder.GetSnapshots();
                result.ProgramError = session.GetStderr();
                session.Dispose();
            }

            return result;
        }

        private static void FinishAfterGameOver(JudgeResult result, StudentSession session, SnakeState state, RunOptions options, LimitedRunRecorder recorder)
        {
            List<string> finalLines = new List<string>();
            for (int i = 0; i < 21; i++)
            {
                string line;
                if (!session.TryReadLine(options.FinalTimeoutMs, out line))
                {
                    if (session.LimitStatus != JudgeStatus.NotRun)
                    {
                        SetStatus(result, session.LimitStatus, session.LimitMessage);
                    }
                    else
                    {
                        SetStatus(result, JudgeStatus.TimeLimitExceeded, "OJ 已发送 100 100，但程序没有及时输出最终 20 行地图和 1 行分数。");
                    }
                    result.Score = state.Score;
                    result.Steps = state.MoveCount;
                    return;
                }
                finalLines.Add(line);
            }

            recorder.AppendLine("程序 -> OJ：最终地图与分数");
            for (int i = 0; i < finalLines.Count; i++)
            {
                recorder.AppendLine(finalLines[i]);
            }
            recorder.AppendLine("");

            JudgeStatus finalStatus;
            string mapError;
            if (!CompareFinalOutput(finalLines, state, out finalStatus, out mapError))
            {
                SetStatus(result, finalStatus, mapError);
                result.Score = state.Score;
                result.Steps = state.MoveCount;
                return;
            }

            bool exited = session.WaitForExit(options.FinalTimeoutMs);
            if (!exited)
            {
                SetStatus(result, JudgeStatus.TimeLimitExceeded, "最终地图和分数正确，但程序输出后没有及时结束。题目要求输入结束后立刻结束程序。");
                result.Score = state.Score;
                result.Steps = state.MoveCount;
                return;
            }

            if (session.ExitCode != 0)
            {
                SetStatus(result, JudgeStatus.RuntimeError, "最终输出正确，但程序退出码为 " + session.ExitCode + "。请检查是否有异常结束。");
                result.Score = state.Score;
                result.Steps = state.MoveCount;
                return;
            }

            SetStatus(result, JudgeStatus.Accepted, "通过：交互格式、最终地图和得分均正确。");
            result.Score = state.Score;
            result.Steps = state.MoveCount;
        }

        private static bool CompareFinalOutput(List<string> finalLines, SnakeState state, out JudgeStatus status, out string error)
        {
            status = JudgeStatus.WrongAnswer;
            error = "";
            if (finalLines.Count != 21)
            {
                status = JudgeStatus.PresentationError;
                error = "最终输出行数不足：应输出 20 行地图和 1 行分数。";
                return false;
            }

            string[] expected = state.ToLines();
            for (int r = 0; r < 20; r++)
            {
                string actual = finalLines[r];
                if (actual == null || actual.Length != 20)
                {
                    status = JudgeStatus.PresentationError;
                    error = "最终地图第 " + r + " 行长度错误：应为 20，实际为 " + (actual == null ? 0 : actual.Length) + "。";
                    return false;
                }
                if (actual != expected[r])
                {
                    int diff = FirstDiff(expected[r], actual);
                    status = JudgeStatus.WrongAnswer;
                    error = "最终地图不一致：第 " + r + " 行第 " + diff + " 列应为 `" + expected[r][diff] + "`，实际为 `" + actual[diff] + "`。";
                    return false;
                }
            }

            int finalScore;
            if (!int.TryParse(finalLines[20].Trim(), out finalScore))
            {
                status = JudgeStatus.PresentationError;
                error = "最终分数格式错误：第 21 行应为整数，实际为 `" + finalLines[20] + "`。";
                return false;
            }

            if (finalScore != state.Score)
            {
                status = JudgeStatus.WrongAnswer;
                error = "最终分数错误：真实分数是 " + state.Score + "，程序输出了 " + finalScore + "。";
                return false;
            }

            return true;
        }

        private static int FirstDiff(string expected, string actual)
        {
            int len = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < len; i++)
            {
                if (expected[i] != actual[i])
                {
                    return i;
                }
            }
            return len;
        }

        private static void SetStatus(JudgeResult result, JudgeStatus status, string message)
        {
            result.Status = status;
            result.StatusText = ToChineseStatus(status);
            result.Message = message;
        }

        internal static string TimeLimitMessage(RunOptions options, long exceededAtMs, long usedMs, bool stoppedAtDiagnosticLimit)
        {
            int diagnosticLimit = DiagnosticLimitMs(options);
            if (stoppedAtDiagnosticLimit)
            {
                return "时间超限：本地限制为 " + options.TimeLimitMs + "ms，程序在约 " + exceededAtMs + "ms 时已超时；测试器已继续观察到诊断上限 " + diagnosticLimit + "ms 仍未完成，已强制停止。若想查看实际分数，请在“高级设置”里调高时间限制后重测。";
            }
            return "时间超限：本地限制为 " + options.TimeLimitMs + "ms，程序在约 " + exceededAtMs + "ms 时已超时；测试器为了诊断继续运行到约 " + usedMs + "ms。本次分数列显示延长运行得到的分数，正式判题仍为超时。";
        }

        internal static int DiagnosticLimitMs(RunOptions options)
        {
            if (options == null)
            {
                return 3000;
            }
            int observation = options.TimeoutObservationMs > 0 ? options.TimeoutObservationMs : 3000;
            if (options.TimeLimitMs > observation)
            {
                return options.TimeLimitMs;
            }
            return observation;
        }

        public static string ToChineseStatus(JudgeStatus status)
        {
            if (status == JudgeStatus.Accepted) return "通过";
            if (status == JudgeStatus.WrongAnswer) return "答案错误";
            if (status == JudgeStatus.PresentationError) return "输出格式错误";
            if (status == JudgeStatus.TimeLimitExceeded) return "超时";
            if (status == JudgeStatus.RuntimeError) return "运行错误";
            if (status == JudgeStatus.CompileError) return "编译失败";
            if (status == JudgeStatus.MemoryLimitExceeded) return "内存超限";
            if (status == JudgeStatus.CodeLengthExceeded) return "代码过长";
            return "未运行";
        }

        private static string Quote(string path)
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        private static string SourceFingerprint(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        hash ^= text[i];
                        hash *= 16777619u;
                    }
                }
                return hash.ToString("X8");
            }
        }

        private static void TryKill(Process p)
        {
            try
            {
                if (p != null && !p.HasExited)
                {
                    p.Kill();
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class MoveOutcome
    {
        public bool Ended;
        public string EndReason;
        public bool AteFood;
        public bool Grew;
        public string Response;
        public string Note;

        public MoveOutcome()
        {
            EndReason = "";
            Response = "20 20";
            Note = "";
        }
    }

    internal sealed class LimitedRunRecorder
    {
        private readonly int _maxRecentSnapshots;
        private readonly int _maxLogChars;
        private readonly bool _captureDetails;
        private readonly int _snapshotInterval;
        private readonly List<GridSnapshot> _prefixSnapshots;
        private readonly Queue<GridSnapshot> _recentSnapshots;
        private readonly StringBuilder _log;
        private int _droppedSnapshots;
        private int _droppedLogLines;
        private bool _logLimitNoted;

        public LimitedRunRecorder(bool captureDetails, int maxSnapshotsToKeep, int maxLogChars, int snapshotInterval)
        {
            _captureDetails = captureDetails;
            _snapshotInterval = Math.Max(1, snapshotInterval);
            if (maxSnapshotsToKeep < 2)
            {
                maxSnapshotsToKeep = 2;
            }
            _maxRecentSnapshots = maxSnapshotsToKeep - 1;
            _maxLogChars = Math.Max(2000, maxLogChars);
            _prefixSnapshots = new List<GridSnapshot>();
            _recentSnapshots = new Queue<GridSnapshot>();
            _log = new StringBuilder(Math.Min(_maxLogChars, 8192));
        }

        public bool CaptureDetails
        {
            get { return _captureDetails; }
        }

        public bool ShouldCaptureSnapshot(int step, bool important)
        {
            if (!_captureDetails)
            {
                return false;
            }
            if (important)
            {
                return true;
            }
            return step > 0 && (step % _snapshotInterval) == 0;
        }

        public void AddSnapshot(GridSnapshot snapshot)
        {
            if (!_captureDetails || snapshot == null)
            {
                return;
            }

            if (_prefixSnapshots.Count == 0)
            {
                _prefixSnapshots.Add(snapshot);
                return;
            }

            _recentSnapshots.Enqueue(snapshot);
            while (_recentSnapshots.Count > _maxRecentSnapshots)
            {
                _recentSnapshots.Dequeue();
                _droppedSnapshots++;
            }
        }

        public List<GridSnapshot> GetSnapshots()
        {
            List<GridSnapshot> snapshots = new List<GridSnapshot>();
            if (!_captureDetails)
            {
                return snapshots;
            }
            snapshots.AddRange(_prefixSnapshots);
            snapshots.AddRange(_recentSnapshots.ToArray());
            if (_droppedSnapshots > 0 && snapshots.Count > 0)
            {
                snapshots[0].Note = AppendNote(snapshots[0].Note, "为保持界面流畅，中间 " + _droppedSnapshots + " 个地图快照未保留；判题分数不受影响。");
            }
            return snapshots;
        }

        public void AppendLine(string line)
        {
            if (!_captureDetails)
            {
                return;
            }
            if (line == null)
            {
                line = "";
            }

            int needed = line.Length + Environment.NewLine.Length;
            if (_log.Length + needed <= _maxLogChars)
            {
                _log.AppendLine(line);
                return;
            }

            _droppedLogLines++;
            if (!_logLimitNoted)
            {
                _logLimitNoted = true;
                _log.AppendLine();
                _log.AppendLine("[日志过长，后续交互日志已省略；判题分数不受影响。]");
            }
        }

        public string GetLog()
        {
            if (!_captureDetails)
            {
                return "跑分评分模式未记录逐步交互日志，以保持界面流畅；判题和分数不受影响。";
            }
            if (_droppedLogLines > 0)
            {
                _log.AppendLine("[共省略 " + _droppedLogLines + " 行交互日志。]");
            }
            return _log.ToString();
        }

        private static string AppendNote(string original, string addition)
        {
            if (string.IsNullOrEmpty(original))
            {
                return addition;
            }
            return original + " " + addition;
        }
    }

    internal sealed class SnakeState
    {
        private char[,] _terrain;
        private List<Point> _snake;
        private Point _food;
        private int _n;
        private Direction _direction;

        public int Score;
        public int MoveCount;

        private SnakeState()
        {
            _terrain = new char[20, 20];
            _snake = new List<Point>();
            _food = new Point(-1, -1);
            _direction = Direction.Up;
        }

        public static SnakeState FromTestCase(TestCase testCase)
        {
            SnakeState state = new SnakeState();
            state._n = testCase.N;
            List<Point> body = new List<Point>();
            Point head = new Point(-1, -1);

            for (int r = 0; r < 20; r++)
            {
                for (int c = 0; c < 20; c++)
                {
                    char ch = testCase.InitialMap[r][c];
                    if (ch == 'H')
                    {
                        head = new Point(c, r);
                        state._terrain[r, c] = '.';
                    }
                    else if (ch == 'B')
                    {
                        body.Add(new Point(c, r));
                        state._terrain[r, c] = '.';
                    }
                    else if (ch == 'F')
                    {
                        state._food = new Point(c, r);
                        state._terrain[r, c] = '.';
                    }
                    else
                    {
                        state._terrain[r, c] = ch;
                    }
                }
            }

            if (head.X < 0)
            {
                throw new InvalidOperationException("地图中缺少蛇头 H。");
            }
            if (state._food.X < 0)
            {
                throw new InvalidOperationException("地图中缺少食物 F。");
            }

            state._snake = OrderSnake(head, body);
            return state;
        }

        public MoveOutcome ApplyMove(Direction nextDirection, CStyleRandom random)
        {
            MoveOutcome outcome = new MoveOutcome();
            if (DirectionHelper.IsReverse(_direction, nextDirection))
            {
                outcome.Ended = true;
                outcome.EndReason = "蛇不能从当前方向 `" + DirectionHelper.ChineseName(_direction) + "` 直接反向到 `" + DirectionHelper.ChineseName(nextDirection) + "`。";
                return outcome;
            }

            Point head = _snake[0];
            Point delta = DirectionHelper.Delta(nextDirection);
            Point nextHead = new Point(head.X + delta.X, head.Y + delta.Y);
            bool ateFood = Same(nextHead, _food);
            bool naturalGrow = _n > 0 && ((MoveCount + 1) % _n == 0);
            bool grow = ateFood || naturalGrow;

            if (nextHead.X < 0 || nextHead.Y < 0 || nextHead.X >= 20 || nextHead.Y >= 20)
            {
                outcome.Ended = true;
                outcome.EndReason = "蛇头移动到地图外：" + PosText(nextHead) + "。";
                return outcome;
            }

            char terrain = _terrain[nextHead.Y, nextHead.X];
            if (terrain == '#')
            {
                outcome.Ended = true;
                outcome.EndReason = "蛇头撞墙：" + PosText(nextHead) + "。";
                return outcome;
            }
            if (terrain == 'O')
            {
                outcome.Ended = true;
                outcome.EndReason = "蛇头撞到障碍物：" + PosText(nextHead) + "。";
                return outcome;
            }

            int tailIndex = _snake.Count - 1;
            for (int i = 0; i < _snake.Count; i++)
            {
                if (Same(_snake[i], nextHead))
                {
                    if (i == tailIndex && !grow)
                    {
                        continue;
                    }
                    outcome.Ended = true;
                    outcome.EndReason = "蛇头撞到自身：" + PosText(nextHead) + "。";
                    return outcome;
                }
            }

            _snake.Insert(0, nextHead);
            if (!grow)
            {
                _snake.RemoveAt(_snake.Count - 1);
            }

            MoveCount++;
            _direction = nextDirection;
            outcome.AteFood = ateFood;
            outcome.Grew = grow;

            if (ateFood)
            {
                Score += 10;
                _food = PickFood(random);
                if (_food.X >= 0)
                {
                    outcome.Response = _food.Y + " " + _food.X;
                    outcome.Note = "吃到食物，得分 +10，新食物生成在 " + PosText(_food) + "。";
                }
                else
                {
                    outcome.Response = "20 20";
                    outcome.Note = "吃到食物，得分 +10，但地图已无空位生成新食物。";
                }
            }
            else
            {
                outcome.Response = "20 20";
                if (naturalGrow)
                {
                    outcome.Note = "达到 N=" + _n + " 的自然增长步，尾部保留一节。";
                }
                else
                {
                    outcome.Note = "正常移动。";
                }
            }

            if (ateFood && naturalGrow)
            {
                outcome.Note += " 本步同时满足吃食物和自然增长，按题意只增长一节。";
            }

            return outcome;
        }

        public GridSnapshot CreateSnapshot(string caseName, string studentMove, string ojResponse, bool ateFood, bool grew, bool ended, string endReason, string note)
        {
            GridSnapshot snapshot = new GridSnapshot();
            snapshot.CaseName = caseName;
            snapshot.Step = MoveCount;
            snapshot.Score = Score;
            snapshot.N = _n;
            snapshot.SnakeLength = _snake.Count;
            snapshot.Head = _snake.Count == 0 ? new Point(-1, -1) : _snake[0];
            snapshot.Food = _food;
            snapshot.CurrentDirection = _direction;
            snapshot.StudentMove = studentMove == null ? "" : studentMove.Trim();
            snapshot.OjResponse = ojResponse == null ? "" : ojResponse.Trim();
            snapshot.AteFood = ateFood;
            snapshot.Grew = grew;
            snapshot.Ended = ended;
            snapshot.EndReason = endReason == null ? "" : endReason;
            snapshot.Note = note == null ? "" : note;
            snapshot.Map = ToMap();
            return snapshot;
        }

        public string[] ToLines()
        {
            char[,] map = ToMap();
            string[] lines = new string[20];
            for (int r = 0; r < 20; r++)
            {
                char[] chars = new char[20];
                for (int c = 0; c < 20; c++)
                {
                    chars[c] = map[r, c];
                }
                lines[r] = new string(chars);
            }
            return lines;
        }

        private char[,] ToMap()
        {
            char[,] map = new char[20, 20];
            for (int r = 0; r < 20; r++)
            {
                for (int c = 0; c < 20; c++)
                {
                    map[r, c] = _terrain[r, c];
                }
            }

            if (_food.X >= 0)
            {
                map[_food.Y, _food.X] = 'F';
            }

            for (int i = _snake.Count - 1; i >= 0; i--)
            {
                Point p = _snake[i];
                map[p.Y, p.X] = i == 0 ? 'H' : 'B';
            }
            return map;
        }

        private Point PickFood(CStyleRandom random)
        {
            for (int guard = 0; guard < 1000; guard++)
            {
                int r = 1 + random.NextInt(18);
                int c = 1 + random.NextInt(18);
                if (IsFreeForFood(c, r))
                {
                    return new Point(c, r);
                }
            }

            for (int r = 1; r < 19; r++)
            {
                for (int c = 1; c < 19; c++)
                {
                    if (IsFreeForFood(c, r))
                    {
                        return new Point(c, r);
                    }
                }
            }
            return new Point(-1, -1);
        }

        private bool IsFreeForFood(int col, int row)
        {
            if (_terrain[row, col] != '.')
            {
                return false;
            }
            if (_food.X == col && _food.Y == row)
            {
                return false;
            }
            for (int i = 0; i < _snake.Count; i++)
            {
                if (_snake[i].X == col && _snake[i].Y == row)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<Point> OrderSnake(Point head, List<Point> body)
        {
            List<Point> ordered = new List<Point>();
            ordered.Add(head);
            List<Point> remain = new List<Point>(body);
            Point current = head;

            while (remain.Count > 0)
            {
                int found = -1;
                for (int i = 0; i < remain.Count; i++)
                {
                    if (Adjacent(current, remain[i]))
                    {
                        found = i;
                        break;
                    }
                }

                if (found < 0)
                {
                    throw new InvalidOperationException("初始蛇身不连续，无法确定蛇身顺序。");
                }

                Point next = remain[found];
                ordered.Add(next);
                remain.RemoveAt(found);
                current = next;
            }

            return ordered;
        }

        private static bool Same(Point a, Point b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        private static bool Adjacent(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
        }

        private static string PosText(Point p)
        {
            return "(" + p.Y + ", " + p.X + ")";
        }
    }

    internal sealed class StudentSession : IDisposable
    {
        private Process _process;
        private ConcurrentQueue<string> _stdoutLines;
        private AutoResetEvent _lineEvent;
        private StringBuilder _stderr;
        private object _stderrLock;
        private Thread _monitorThread;
        private RunOptions _options;
        private volatile bool _disposed;
        private Stopwatch _programTimer;
        private long _programElapsedMs;
        private long _timeLimitExceededAtMs;
        private long _diagnosticLimitReachedAtMs;
        private readonly object _programTimerLock = new object();

        public JudgeStatus LimitStatus;
        public string LimitMessage;

        public StudentSession()
        {
            _stdoutLines = new ConcurrentQueue<string>();
            _lineEvent = new AutoResetEvent(false);
            _stderr = new StringBuilder();
            _stderrLock = new object();
            LimitStatus = JudgeStatus.NotRun;
            LimitMessage = "";
            _programElapsedMs = 0;
            _timeLimitExceededAtMs = 0;
            _diagnosticLimitReachedAtMs = 0;
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process == null || _process.HasExited;
                }
                catch
                {
                    return true;
                }
            }
        }

        public int ExitCode
        {
            get
            {
                try
                {
                    if (_process != null && _process.HasExited)
                    {
                        return _process.ExitCode;
                    }
                }
                catch
                {
                }
                return 0;
            }
        }

        public long ProgramElapsedMs
        {
            get
            {
                lock (_programTimerLock)
                {
                    if (_programTimer != null && _programTimer.IsRunning)
                    {
                        return _programTimer.ElapsedMilliseconds;
                    }
                    return _programElapsedMs;
                }
            }
        }

        public long TimeLimitExceededAtMs
        {
            get { return Interlocked.Read(ref _timeLimitExceededAtMs); }
        }

        public bool StoppedAtDiagnosticLimit
        {
            get { return Interlocked.Read(ref _diagnosticLimitReachedAtMs) > 0; }
        }

        public long DiagnosticLimitMs
        {
            get { return EffectiveDiagnosticLimitMs(); }
        }

        public void Start(string exePath, string workingDirectory, RunOptions options)
        {
            _options = options;
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exePath;
            psi.WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Path.GetDirectoryName(exePath) : workingDirectory;
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            _process = new Process();
            _process.StartInfo = psi;
            _process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    _stdoutLines.Enqueue(e.Data);
                    _lineEvent.Set();
                }
            };
            _process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    lock (_stderrLock)
                    {
                        _stderr.AppendLine(e.Data);
                    }
                }
            };

            _process.Start();
            StartProgramTimer();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _monitorThread = new Thread(MonitorLoop);
            _monitorThread.IsBackground = true;
            _monitorThread.Start();
        }

        public void Write(string text)
        {
            if (_process == null || _process.HasExited)
            {
                return;
            }
            _process.StandardInput.Write(text);
            _process.StandardInput.Flush();
        }

        public void WriteLine(string text)
        {
            if (_process == null || _process.HasExited)
            {
                return;
            }
            _process.StandardInput.WriteLine(text);
            _process.StandardInput.Flush();
        }

        public bool TryReadLine(int timeoutMs, out string line)
        {
            line = null;
            Stopwatch wait = Stopwatch.StartNew();
            int effectiveTimeout = EffectiveReadTimeoutMs(timeoutMs);
            while (wait.ElapsedMilliseconds < effectiveTimeout)
            {
                if (_stdoutLines.TryDequeue(out line))
                {
                    return true;
                }

                if (LimitStatus != JudgeStatus.NotRun)
                {
                    return false;
                }

                if (HasExited)
                {
                    if (_stdoutLines.TryDequeue(out line))
                    {
                        return true;
                    }
                    return false;
                }

                int left = effectiveTimeout - (int)wait.ElapsedMilliseconds;
                if (left <= 0)
                {
                    break;
                }
                _lineEvent.WaitOne(Math.Min(10, left));
            }
            return _stdoutLines.TryDequeue(out line);
        }

        public string GetStderr()
        {
            lock (_stderrLock)
            {
                return _stderr.ToString();
            }
        }

        public bool WaitForExit(int timeoutMs)
        {
            if (_process == null)
            {
                return true;
            }

            bool exited;
            try
            {
                int effectiveTimeout = TimeLimitExceededAtMs > 0 ? RemainingDiagnosticTimeMs() : Math.Max(1, timeoutMs);
                exited = _process.WaitForExit(effectiveTimeout);
            }
            catch
            {
                exited = true;
            }

            if (exited)
            {
                StopProgramTimer();
            }
            return exited;
        }

        private void MonitorLoop()
        {
            while (!_disposed)
            {
                try
                {
                    if (_process == null)
                    {
                        return;
                    }
                    if (_process.HasExited)
                    {
                        StopProgramTimer();
                        return;
                    }

                    long usedMs = ProgramElapsedMs;
                    if (_options != null && _options.TimeLimitMs > 0 && usedMs > _options.TimeLimitMs)
                    {
                        MarkTimeLimitExceeded(usedMs);
                    }

                    int diagnosticLimit = EffectiveDiagnosticLimitMs();
                    if (diagnosticLimit > 0 && usedMs > diagnosticLimit)
                    {
                        long exceededAtMs = TimeLimitExceededAtMs;
                        if (exceededAtMs <= 0)
                        {
                            exceededAtMs = usedMs;
                            MarkTimeLimitExceeded(usedMs);
                        }
                        Interlocked.CompareExchange(ref _diagnosticLimitReachedAtMs, usedMs, 0);
                        LimitStatus = JudgeStatus.TimeLimitExceeded;
                        LimitMessage = JudgeEngine.TimeLimitMessage(_options, exceededAtMs, usedMs, true);
                        TryKillProcessTree();
                        StopProgramTimer();
                        _lineEvent.Set();
                        return;
                    }

                    if (_options != null && _options.MemoryLimitKb > 0)
                    {
                        long memoryBytes = 0;
                        try
                        {
                            _process.Refresh();
                            memoryBytes = _process.PrivateMemorySize64;
                        }
                        catch
                        {
                            memoryBytes = 0;
                        }
                        if (memoryBytes > (long)_options.MemoryLimitKb * 1024L)
                        {
                            LimitStatus = JudgeStatus.MemoryLimitExceeded;
                            LimitMessage = "内存超限：本地限制为 " + (_options.MemoryLimitKb / 1024) + "MB，当前私有内存约 " + (memoryBytes / 1024 / 1024) + "MB。";
                            TryKillProcessTree();
                            StopProgramTimer();
                            _lineEvent.Set();
                            return;
                        }
                    }
                }
                catch
                {
                }

                int interval = 20;
                if (_options != null && _options.LimitCheckIntervalMs > 0)
                {
                    interval = _options.LimitCheckIntervalMs;
                }
                Thread.Sleep(Math.Max(5, interval));
            }
        }

        private int EffectiveReadTimeoutMs(int requestedTimeoutMs)
        {
            int remaining = RemainingDiagnosticTimeMs();
            if (remaining > 0)
            {
                return remaining;
            }
            return Math.Max(1, requestedTimeoutMs);
        }

        private int RemainingDiagnosticTimeMs()
        {
            int diagnosticLimit = EffectiveDiagnosticLimitMs();
            if (diagnosticLimit <= 0)
            {
                return 0;
            }
            long remaining = diagnosticLimit - ProgramElapsedMs;
            if (remaining <= 0)
            {
                return 1;
            }
            if (remaining > int.MaxValue)
            {
                return int.MaxValue;
            }
            return (int)remaining;
        }

        private int EffectiveDiagnosticLimitMs()
        {
            return JudgeEngine.DiagnosticLimitMs(_options);
        }

        private void MarkTimeLimitExceeded(long usedMs)
        {
            if (Interlocked.CompareExchange(ref _timeLimitExceededAtMs, usedMs, 0) == 0)
            {
                _lineEvent.Set();
            }
        }

        private void StartProgramTimer()
        {
            lock (_programTimerLock)
            {
                _programElapsedMs = 0;
                _programTimer = Stopwatch.StartNew();
            }
        }

        private void StopProgramTimer()
        {
            lock (_programTimerLock)
            {
                if (_programTimer != null)
                {
                    if (_programTimer.IsRunning)
                    {
                        _programTimer.Stop();
                    }
                    _programElapsedMs = _programTimer.ElapsedMilliseconds;
                }
            }
        }

        private void TryKillProcess()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch
            {
            }
        }

        private void TryKillProcessTree()
        {
            int pid = 0;
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    pid = _process.Id;
                }
            }
            catch
            {
                pid = 0;
            }

            if (pid > 0)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "taskkill.exe";
                    psi.Arguments = "/PID " + pid + " /T /F";
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (Process killer = Process.Start(psi))
                    {
                        if (killer != null)
                        {
                            killer.WaitForExit(1000);
                        }
                    }
                }
                catch
                {
                }
            }

            TryKillProcess();
        }

        public void Dispose()
        {
            _disposed = true;
            StopProgramTimer();
            TryKillProcessTree();
            try
            {
                if (_lineEvent != null)
                {
                    _lineEvent.Set();
                    _lineEvent.Dispose();
                }
            }
            catch
            {
            }
            try
            {
                if (_process != null)
                {
                    _process.Dispose();
                }
            }
            catch
            {
            }
        }
    }

}
