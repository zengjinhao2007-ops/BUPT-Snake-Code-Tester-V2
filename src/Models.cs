using System;
using System.Collections.Generic;
using System.Drawing;

namespace SnakeOJTester
{
    public enum Direction
    {
        Up,
        Left,
        Down,
        Right
    }

    public enum JudgeStatus
    {
        NotRun,
        Accepted,
        WrongAnswer,
        PresentationError,
        TimeLimitExceeded,
        RuntimeError,
        CompileError,
        MemoryLimitExceeded,
        CodeLengthExceeded
    }

    public sealed class TestCase
    {
        public string Name;
        public string Description;
        public string[] InitialMap;
        public int N;
        public int Seed;
        public bool IsScoringCase;

        public TestCase()
        {
            Name = "";
            Description = "";
            InitialMap = new string[20];
            N = 32;
            Seed = 1;
        }

        public string GetInitialInput()
        {
            List<string> lines = new List<string>();
            lines.AddRange(InitialMap);
            lines.Add(N.ToString());
            return string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine;
        }
    }

    public sealed class GridSnapshot
    {
        public string CaseName;
        public int Step;
        public int Score;
        public int N;
        public int SnakeLength;
        public Point Head;
        public Point Food;
        public Direction CurrentDirection;
        public string StudentMove;
        public string OjResponse;
        public bool AteFood;
        public bool Grew;
        public bool Ended;
        public string EndReason;
        public string Note;
        public char[,] Map;

        public GridSnapshot()
        {
            CaseName = "";
            Food = new Point(-1, -1);
            StudentMove = "";
            OjResponse = "";
            EndReason = "";
            Note = "";
            Map = new char[20, 20];
        }

        public string[] ToLines()
        {
            string[] lines = new string[20];
            for (int r = 0; r < 20; r++)
            {
                char[] chars = new char[20];
                for (int c = 0; c < 20; c++)
                {
                    chars[c] = Map[r, c];
                }
                lines[r] = new string(chars);
            }
            return lines;
        }
    }

    public sealed class JudgeResult
    {
        public string CaseName;
        public int CaseIndex;
        public int N;
        public JudgeStatus Status;
        public string StatusText;
        public int Score;
        public int Steps;
        public long ElapsedMs;
        public long ProgramElapsedMs;
        public long TimeLimitExceededAtMs;
        public long DiagnosticLimitMs;
        public bool StoppedAtDiagnosticLimit;
        public string Message;
        public string InteractionLog;
        public string ProgramError;
        public List<GridSnapshot> Snapshots;

        public JudgeResult()
        {
            CaseName = "";
            CaseIndex = -1;
            Status = JudgeStatus.NotRun;
            StatusText = "未运行";
            Message = "";
            ElapsedMs = 0;
            ProgramElapsedMs = 0;
            TimeLimitExceededAtMs = 0;
            DiagnosticLimitMs = 0;
            StoppedAtDiagnosticLimit = false;
            InteractionLog = "";
            ProgramError = "";
            Snapshots = new List<GridSnapshot>();
        }
    }

    public sealed class CompileResult
    {
        public bool Success;
        public string ExePath;
        public string SourcePath;
        public string SourceFingerprint;
        public string Message;
        public string CompilerOutput;

        public CompileResult()
        {
            ExePath = "";
            SourcePath = "";
            SourceFingerprint = "";
            Message = "";
            CompilerOutput = "";
        }
    }

    public sealed class RunOptions
    {
        public int LineTimeoutMs;
        public int FinalTimeoutMs;
        public int CompileTimeoutMs;
        public int TimeLimitMs;
        public int MemoryLimitKb;
        public int CodeLengthLimitKb;
        public int StackLimitKb;
        public bool CaptureDetails;
        public int MaxSnapshotsToKeep;
        public int MaxLogChars;
        public int LimitCheckIntervalMs;
        public int SnapshotInterval;
        public int TimeoutObservationMs;

        public RunOptions()
        {
            LineTimeoutMs = 1000;
            FinalTimeoutMs = 1000;
            CompileTimeoutMs = 12000;
            TimeLimitMs = 1000;
            MemoryLimitKb = 64 * 1024;
            CodeLengthLimitKb = 32;
            StackLimitKb = 8192;
            CaptureDetails = true;
            MaxSnapshotsToKeep = 300;
            MaxLogChars = 120000;
            LimitCheckIntervalMs = 20;
            SnapshotInterval = 1;
            TimeoutObservationMs = 3000;
        }
    }

    public static class DirectionHelper
    {
        public static bool TryParse(string text, out Direction direction)
        {
            direction = Direction.Up;
            if (text == null)
            {
                return false;
            }
            text = text.Trim();
            if (text.Length != 1)
            {
                return false;
            }
            char ch = text[0];
            if (ch == 'W')
            {
                direction = Direction.Up;
                return true;
            }
            if (ch == 'A')
            {
                direction = Direction.Left;
                return true;
            }
            if (ch == 'S')
            {
                direction = Direction.Down;
                return true;
            }
            if (ch == 'D')
            {
                direction = Direction.Right;
                return true;
            }
            return false;
        }

        public static string ToOjChar(Direction direction)
        {
            if (direction == Direction.Up) return "W";
            if (direction == Direction.Left) return "A";
            if (direction == Direction.Down) return "S";
            return "D";
        }

        public static bool IsReverse(Direction current, Direction next)
        {
            return (current == Direction.Up && next == Direction.Down)
                || (current == Direction.Down && next == Direction.Up)
                || (current == Direction.Left && next == Direction.Right)
                || (current == Direction.Right && next == Direction.Left);
        }

        public static Point Delta(Direction direction)
        {
            if (direction == Direction.Up) return new Point(0, -1);
            if (direction == Direction.Down) return new Point(0, 1);
            if (direction == Direction.Left) return new Point(-1, 0);
            return new Point(1, 0);
        }

        public static string ChineseName(Direction direction)
        {
            if (direction == Direction.Up) return "上";
            if (direction == Direction.Down) return "下";
            if (direction == Direction.Left) return "左";
            return "右";
        }
    }
}
