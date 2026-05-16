using System;
using System.Collections.Generic;
using System.Drawing;

namespace SnakeOJTester
{
    public static class TestCaseFactory
    {
        private static readonly int[] ScoringNs = new int[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 };

        public static List<TestCase> CreateDefaultCases()
        {
            List<TestCase> cases = new List<TestCase>();
            cases.Add(CreateOfficialSample());
            cases.Add(CreateImmediateFoodCase());
            cases.Add(CreateWallPressureCase());
            cases.Add(CreateObstacleBeltCase());
            cases.Add(CreateNaturalGrowthCase());
            cases.Add(CreateTailAndSelfCase());
            cases.Add(CreateDenseCorridorCase());

            int[] ns = new int[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 };
            for (int i = 0; i < ns.Length; i++)
            {
                cases.Add(CreateRandomOjLikeCase(20260506 + i * 97, ns[i], "跑分用例-" + (i + 1).ToString("00") + "  N=" + ns[i], true));
            }

            for (int i = 0; i < 8; i++)
            {
                int n = 3 + i * 5;
                cases.Add(CreateRandomOjLikeCase(9000 + i * 131, n, "压力测试-" + (i + 1).ToString("00") + "  N=" + n, false));
            }

            return cases;
        }

        public static List<TestCase> CreateRandomScoringCases(int groupSeed)
        {
            List<TestCase> cases = new List<TestCase>();
            for (int i = 0; i < ScoringNs.Length; i++)
            {
                int seed = unchecked(groupSeed + (i + 1) * 1009);
                if (seed == int.MinValue)
                {
                    seed = 1009 + i;
                }
                if (seed < 0)
                {
                    seed = -seed;
                }
                if (seed == 0)
                {
                    seed = 1009 + i;
                }
                cases.Add(CreateRandomOjLikeCase(seed, ScoringNs[i], "随机跑分用例-" + (i + 1).ToString("00") + "  N=" + ScoringNs[i], true));
            }
            return cases;
        }

        private static TestCase CreateOfficialSample()
        {
            string[] map = new string[]
            {
                "####################",
                "#..................#",
                "#..................#",
                "#...OOOOOOOOOO.....#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#...BHF............#",
                "#...B..............#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "#..................#",
                "####################"
            };
            return CreateCase("题面交互样例", "来自题面文档的交互样例，适合检查基本协议。", map, 32, 42, 600);
        }

        private static TestCase CreateImmediateFoodCase()
        {
            char[,] map = EmptyMap();
            AddObstacleLine(map, 4, 4, 10);
            PlaceSnake(map, new Point[] { P(10, 10), P(11, 10), P(12, 10) });
            map[10, 11] = 'F';
            return CreateCase("立即吃食物 N=1", "第一步即可吃到食物，同时 N=1，用于检查同一步只增长一节。", ToLines(map), 1, 31, 500);
        }

        private static TestCase CreateWallPressureCase()
        {
            char[,] map = EmptyMap();
            AddObstacleLine(map, 8, 3, 10);
            PlaceSnake(map, new Point[] { P(1, 10), P(2, 10), P(3, 10) });
            map[1, 16] = 'F';
            return CreateCase("贴墙起步", "蛇头靠近上墙，错误策略很容易撞墙。", ToLines(map), 8, 41, 500);
        }

        private static TestCase CreateObstacleBeltCase()
        {
            char[,] map = EmptyMap();
            AddObstacleLine(map, 6, 3, 10);
            PlaceSnake(map, new Point[] { P(10, 5), P(11, 5), P(12, 5) });
            map[4, 15] = 'F';
            return CreateCase("障碍带绕行", "10 个连续障碍物形成绕行压力，检查避障能力。", ToLines(map), 16, 51, 700);
        }

        private static TestCase CreateNaturalGrowthCase()
        {
            char[,] map = EmptyMap();
            map[3, 4] = 'O';
            map[3, 5] = 'O';
            map[3, 6] = 'O';
            map[8, 12] = 'O';
            map[9, 12] = 'O';
            map[10, 12] = 'O';
            map[12, 3] = 'O';
            map[12, 4] = 'O';
            map[12, 5] = 'O';
            map[12, 6] = 'O';
            PlaceSnake(map, new Point[] { P(15, 15), P(16, 15), P(17, 15) });
            map[2, 15] = 'F';
            return CreateCase("自然增长 N=3", "每 3 步自然增长一次，用于检查尾部增长和自撞判断。", ToLines(map), 3, 61, 750);
        }

        private static TestCase CreateTailAndSelfCase()
        {
            char[,] map = EmptyMap();
            for (int c = 5; c <= 14; c++) map[3, c] = 'O';
            Point[] snake = new Point[]
            {
                P(10, 10), P(11, 10), P(12, 10)
            };
            PlaceSnake(map, snake);
            map[6, 14] = 'F';
            return CreateCase("长蛇自撞压力", "初始蛇长保持题面要求的 3 节，通过 N=5 的自然增长制造长蛇和自撞压力。", ToLines(map), 5, 71, 900);
        }

        private static TestCase CreateDenseCorridorCase()
        {
            char[,] map = EmptyMap();
            for (int r = 2; r <= 11; r++) map[r, 4] = 'O';
            PlaceSnake(map, new Point[] { P(14, 8), P(15, 8), P(16, 8) });
            map[2, 16] = 'F';
            return CreateCase("竖向走廊", "10 个障碍物形成竖向隔断，测试寻路和容错。", ToLines(map), 12, 81, 800);
        }

        private static TestCase CreateRandomOjLikeCase(int seed, int n, string name, bool scoringCase)
        {
            CStyleRandom random = new CStyleRandom(seed);
            char[,] map = EmptyMap();
            Point[] snake = CreateRandomSnake(random);
            PlaceSnake(map, snake);

            int placed = 0;
            while (placed < 10)
            {
                int r = 1 + random.NextInt(18);
                int c = 1 + random.NextInt(18);
                if (map[r, c] == '.')
                {
                    map[r, c] = 'O';
                    placed++;
                }
            }

            Point food = PickFreeCell(map, random);
            map[food.Y, food.X] = 'F';
            return CreateCase(name, "按固定 seed 生成，障碍物 10 个，食物按 C 风格伪随机空格生成。", ToLines(map), n, seed, 1100, scoringCase);
        }

        private static Point[] CreateRandomSnake(CStyleRandom random)
        {
            int row = 7 + random.NextInt(7);
            int col = 7 + random.NextInt(7);
            return new Point[] { P(row, col), P(row + 1, col), P(row + 2, col) };
        }

        private static TestCase CreateCase(string name, string description, string[] map, int n, int seed, int maxSteps)
        {
            return CreateCase(name, description, map, n, seed, maxSteps, false);
        }

        private static TestCase CreateCase(string name, string description, string[] map, int n, int seed, int maxSteps, bool scoringCase)
        {
            ValidateMap(map);
            TestCase tc = new TestCase();
            tc.Name = name;
            tc.Description = description;
            tc.InitialMap = map;
            tc.N = n;
            tc.Seed = seed;
            tc.IsScoringCase = scoringCase;
            return tc;
        }

        private static char[,] EmptyMap()
        {
            char[,] map = new char[20, 20];
            for (int r = 0; r < 20; r++)
            {
                for (int c = 0; c < 20; c++)
                {
                    map[r, c] = (r == 0 || c == 0 || r == 19 || c == 19) ? '#' : '.';
                }
            }
            return map;
        }

        private static void AddObstacleLine(char[,] map, int row, int colStart, int count)
        {
            for (int i = 0; i < count; i++)
            {
                map[row, colStart + i] = 'O';
            }
        }

        private static void PlaceSnake(char[,] map, Point[] snake)
        {
            for (int i = 0; i < snake.Length; i++)
            {
                Point p = snake[i];
                map[p.Y, p.X] = i == 0 ? 'H' : 'B';
            }
        }

        private static Point PickFreeCell(char[,] map, CStyleRandom random)
        {
            for (int guard = 0; guard < 1000; guard++)
            {
                int r = 1 + random.NextInt(18);
                int c = 1 + random.NextInt(18);
                if (map[r, c] == '.')
                {
                    return P(r, c);
                }
            }

            for (int r = 1; r < 19; r++)
            {
                for (int c = 1; c < 19; c++)
                {
                    if (map[r, c] == '.')
                    {
                        return P(r, c);
                    }
                }
            }
            return P(-1, -1);
        }

        private static string[] ToLines(char[,] map)
        {
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

        private static Point P(int row, int col)
        {
            return new Point(col, row);
        }

        private static void ValidateMap(string[] map)
        {
            if (map == null || map.Length != 20)
            {
                throw new InvalidOperationException("测试地图必须是 20 行。");
            }

            int head = 0;
            int body = 0;
            int food = 0;
            int obstacle = 0;
            Point headPoint = new Point(-1, -1);
            List<Point> bodyPoints = new List<Point>();
            for (int r = 0; r < 20; r++)
            {
                if (map[r] == null || map[r].Length != 20)
                {
                    throw new InvalidOperationException("测试地图每行必须是 20 个字符。");
                }
                for (int c = 0; c < 20; c++)
                {
                    char ch = map[r][c];
                    if (!IsValidMapChar(ch))
                    {
                        throw new InvalidOperationException("测试地图第 " + r + " 行第 " + c + " 列包含非法字符 `" + ch + "`。");
                    }
                    if ((r == 0 || c == 0 || r == 19 || c == 19) && ch != '#')
                    {
                        throw new InvalidOperationException("测试地图边界必须全部为墙 `#`，第 " + r + " 行第 " + c + " 列不符合要求。");
                    }
                    if (ch == 'H')
                    {
                        head++;
                        headPoint = P(r, c);
                    }
                    if (ch == 'B')
                    {
                        body++;
                        bodyPoints.Add(P(r, c));
                    }
                    if (ch == 'F') food++;
                    if (ch == 'O') obstacle++;
                }
            }

            if (head != 1)
            {
                throw new InvalidOperationException("测试地图必须有且只有一个蛇头 H。");
            }
            if (body != 2)
            {
                throw new InvalidOperationException("测试地图必须正好有 2 个蛇身 B，保证初始蛇长为题面要求的 3 节。");
            }
            if (!HasUniqueInitialSnakeOrder(headPoint, bodyPoints))
            {
                throw new InvalidOperationException("测试地图的初始蛇身顺序必须唯一：蛇头 H 应只连接一节蛇身，第二节蛇身应连接在其后。");
            }
            if (food != 1)
            {
                throw new InvalidOperationException("测试地图必须有且只有一个食物 F。");
            }
            if (obstacle != 10)
            {
                throw new InvalidOperationException("测试地图必须正好有 10 个障碍物 O。");
            }
        }

        private static bool IsValidMapChar(char ch)
        {
            return ch == '#'
                || ch == '.'
                || ch == 'O'
                || ch == 'H'
                || ch == 'B'
                || ch == 'F';
        }

        private static bool HasUniqueInitialSnakeOrder(Point head, List<Point> body)
        {
            if (body.Count != 2)
            {
                return false;
            }

            int first = -1;
            for (int i = 0; i < body.Count; i++)
            {
                if (Adjacent(head, body[i]))
                {
                    if (first >= 0)
                    {
                        return false;
                    }
                    first = i;
                }
            }

            if (first < 0)
            {
                return false;
            }

            int second = first == 0 ? 1 : 0;
            return Adjacent(body[first], body[second]);
        }

        private static bool Adjacent(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
        }
    }
}
