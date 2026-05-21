using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SnakeOJTester
{
    public sealed class MainForm : Form
    {
        private const int ScoringSnapshotLimit = 2500;
        private const int ScoringMaxLogChars = 60000;
        private const string FixedScoringGroupLabel = "固定组（当前10组）";
        private const string RandomScoringGroupLabel = "随机组（每次跑分重新生成）";

        private List<TestCase> _cases;
        private List<JudgeResult> _results;
        private string _rootDir;
        private string _workDir;
        private bool _busy;
        private bool _scoreMode;
        private bool _lastRunWasScoring;
        private bool _codeDirtySinceRun;
        private string _lastRunCodeFingerprint;
        private string _lastRunExePath;
        private string _lastScoringGroupLabel;
        private DateTime _lastRunTime;
        private int _lastProgressDone;
        private bool _lastRunUsedRandomScoringGroup;
        private bool _scoreGroupDirtySinceRun;

        private ComboBox _caseCombo;
        private Button _debugModeButton;
        private Button _scoreModeButton;
        private Label _modeHintLabel;
        private Label _scoreGroupHintLabel;
        private ComboBox _scoreGroupCombo;
        private Button _detectGccButton;
        private Button _compileButton;
        private Button _runButton;
        private Button _batchButton;
        private Button _loadButton;
        private Button _saveButton;
        private Button _exportButton;
        private TextBox _codeBox;
        private TextBox _compileBox;
        private TextBox _interactionBox;
        private TextBox _stderrBox;
        private TextBox _caseInfoBox;
        private TextBox _helpBox;
        private Label _scoreSummaryLabel;
        private DataGridView _resultGrid;
        private DataGridView _stepGrid;
        private ListView _variableList;
        private ListView _caseList;
        private MapView _mapView;
        private Label _gccLabel;
        private ToolStripStatusLabel _statusLabel;
        private Label _stepLabel;
        private Button _prevButton;
        private Button _nextButton;
        private Button _playButton;
        private TrackBar _speedTrack;
        private System.Windows.Forms.Timer _playTimer;
        private NumericUpDown _lineTimeoutBox;
        private NumericUpDown _finalTimeoutBox;
        private NumericUpDown _timeLimitBox;
        private NumericUpDown _memoryLimitBox;
        private NumericUpDown _codeLengthLimitBox;
        private NumericUpDown _stackLimitBox;
        private SplitContainer _mainSplit;
        private SplitContainer _leftSplit;
        private SplitContainer _rightSplit;
        private SplitContainer _mapSplit;
        private bool _startupLayoutApplied;

        private JudgeResult _currentResult;
        private int _currentStepIndex;

        public MainForm()
        {
            _cases = TestCaseFactory.CreateDefaultCases();
            _results = new List<JudgeResult>();
            _scoreMode = false;
            _lastRunWasScoring = false;
            _codeDirtySinceRun = false;
            _lastRunCodeFingerprint = "";
            _lastRunExePath = "";
            _lastScoringGroupLabel = FixedScoringGroupLabel;
            _lastProgressDone = 0;
            _lastRunUsedRandomScoringGroup = false;
            _scoreGroupDirtySinceRun = false;
            _startupLayoutApplied = false;
            _rootDir = AppDomain.CurrentDomain.BaseDirectory;
            _workDir = Path.Combine(_rootDir, "work");
            CleanupWorkDirectory();
            Text = "贪吃蛇 OJ 本地测试器";
            Width = 1440;
            Height = 880;
            MinimumSize = new Size(1180, 740);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;

            BuildUi();
            Shown += MainForm_Shown;
            UpdateModeUi();
            UpdateScoreSummary();
            LoadCases();
            ShowInitialPreview();
            _codeBox.Text = "";
            RefreshGccInfo();
            UpdateBusyState(false, "就绪：已加载当前用例初始地图，可直接粘贴 C 代码后运行。");
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            Controls.Add(root);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = true;
            toolbar.AutoScroll = true;
            toolbar.Padding = new Padding(8, 8, 8, 4);
            root.Controls.Add(toolbar, 0, 0);

            toolbar.Controls.Add(new Label { Text = "用例", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 6, 0, 0) });
            _caseCombo = new ComboBox();
            _caseCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _caseCombo.Width = 220;
            _caseCombo.SelectedIndexChanged += delegate
            {
                ShowSelectedCaseInfo();
                ShowInitialPreview();
            };
            toolbar.Controls.Add(_caseCombo);

            TableLayoutPanel modePanel = new TableLayoutPanel();
            modePanel.Width = 360;
            modePanel.Height = 70;
            modePanel.Margin = new Padding(8, 0, 8, 0);
            modePanel.RowCount = 2;
            modePanel.ColumnCount = 2;
            modePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            modePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            modePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            modePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _modeHintLabel = new Label();
            _modeHintLabel.Text = "当前模式：Debug 调试";
            _modeHintLabel.Dock = DockStyle.Fill;
            _modeHintLabel.TextAlign = ContentAlignment.MiddleLeft;
            _modeHintLabel.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
            modePanel.Controls.Add(_modeHintLabel, 0, 0);
            modePanel.SetColumnSpan(_modeHintLabel, 2);

            _debugModeButton = MakeModeButton("Debug 调试", false);
            _scoreModeButton = MakeModeButton("跑分评分", true);
            modePanel.Controls.Add(_debugModeButton, 0, 1);
            modePanel.Controls.Add(_scoreModeButton, 1, 1);
            toolbar.Controls.Add(modePanel);

            _scoreGroupHintLabel = new Label();
            _scoreGroupHintLabel.Text = "跑分组别";
            _scoreGroupHintLabel.AutoSize = true;
            _scoreGroupHintLabel.TextAlign = ContentAlignment.MiddleLeft;
            _scoreGroupHintLabel.Padding = new Padding(8, 6, 0, 0);
            toolbar.Controls.Add(_scoreGroupHintLabel);

            _scoreGroupCombo = new ComboBox();
            _scoreGroupCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _scoreGroupCombo.Width = 220;
            _scoreGroupCombo.Items.Add(FixedScoringGroupLabel);
            _scoreGroupCombo.Items.Add(RandomScoringGroupLabel);
            _scoreGroupCombo.SelectedIndex = 0;
            _scoreGroupCombo.SelectedIndexChanged += ScoringGroupCombo_SelectedIndexChanged;
            toolbar.Controls.Add(_scoreGroupCombo);

            _detectGccButton = MakeButton("检测 GCC", DetectGccButton_Click);
            _compileButton = MakeButton("编译", CompileButton_Click);
            _runButton = MakeButton("运行当前用例", RunButton_Click);
            _batchButton = MakeButton("批量评测", BatchButton_Click);
            _loadButton = MakeButton("加载代码", LoadButton_Click);
            _saveButton = MakeButton("保存代码", SaveButton_Click);
            _exportButton = MakeButton("导出日志", ExportButton_Click);

            toolbar.Controls.Add(_detectGccButton);
            toolbar.Controls.Add(_compileButton);
            toolbar.Controls.Add(_runButton);
            toolbar.Controls.Add(_batchButton);
            toolbar.Controls.Add(_loadButton);
            toolbar.Controls.Add(_saveButton);
            toolbar.Controls.Add(_exportButton);

            _gccLabel = new Label();
            _gccLabel.AutoSize = true;
            _gccLabel.Padding = new Padding(12, 7, 0, 0);
            toolbar.Controls.Add(_gccLabel);

            UpdateModeUi();

            _mainSplit = new SplitContainer();
            _mainSplit.Dock = DockStyle.Fill;
            root.Controls.Add(_mainSplit, 0, 1);

            _leftSplit = new SplitContainer();
            _leftSplit.Dock = DockStyle.Fill;
            _leftSplit.Orientation = Orientation.Horizontal;
            _mainSplit.Panel1.Controls.Add(_leftSplit);

            TabControl codeTabs = new TabControl();
            codeTabs.Dock = DockStyle.Fill;
            _leftSplit.Panel1.Controls.Add(codeTabs);

            TabPage codePage = new TabPage("C 代码");
            _codeBox = new TextBox();
            _codeBox.Multiline = true;
            _codeBox.AcceptsTab = true;
            _codeBox.AcceptsReturn = true;
            _codeBox.ScrollBars = ScrollBars.Both;
            _codeBox.WordWrap = false;
            _codeBox.Font = new Font("Consolas", 10f);
            _codeBox.Dock = DockStyle.Fill;
            _codeBox.TextChanged += CodeBox_TextChanged;
            codePage.Controls.Add(_codeBox);
            codeTabs.TabPages.Add(codePage);

            TabPage casePage = new TabPage("当前用例");
            _caseInfoBox = MakeReadOnlyTextBox();
            casePage.Controls.Add(_caseInfoBox);
            codeTabs.TabPages.Add(casePage);

            _compileBox = MakeReadOnlyTextBox();
            _compileBox.Font = new Font("Consolas", 9.5f);
            _leftSplit.Panel2.Controls.Add(_compileBox);

            _rightSplit = new SplitContainer();
            _rightSplit.Dock = DockStyle.Fill;
            _rightSplit.Orientation = Orientation.Horizontal;
            _mainSplit.Panel2.Controls.Add(_rightSplit);

            _mapSplit = new SplitContainer();
            _mapSplit.Dock = DockStyle.Fill;
            _rightSplit.Panel1.Controls.Add(_mapSplit);

            _mapView = new MapView();
            _mapView.Dock = DockStyle.Fill;
            _mapSplit.Panel1.Controls.Add(_mapView);

            TableLayoutPanel side = new TableLayoutPanel();
            side.Dock = DockStyle.Fill;
            side.RowCount = 3;
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            _mapSplit.Panel2.Controls.Add(side);

            _stepLabel = new Label();
            _stepLabel.Dock = DockStyle.Fill;
            _stepLabel.TextAlign = ContentAlignment.MiddleLeft;
            _stepLabel.Padding = new Padding(8, 0, 8, 0);
            _stepLabel.Text = "快照：未运行";
            side.Controls.Add(_stepLabel, 0, 0);

            _variableList = new ListView();
            _variableList.Dock = DockStyle.Fill;
            _variableList.View = View.Details;
            _variableList.FullRowSelect = true;
            _variableList.GridLines = true;
            _variableList.Columns.Add("变量", 120);
            _variableList.Columns.Add("值", 220);
            side.Controls.Add(_variableList, 0, 1);

            TableLayoutPanel playback = new TableLayoutPanel();
            playback.Dock = DockStyle.Fill;
            playback.Padding = new Padding(8, 4, 8, 4);
            playback.ColumnCount = 5;
            playback.RowCount = 1;
            playback.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            playback.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            playback.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            playback.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            playback.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            playback.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _prevButton = MakeButton("上一步", PrevButton_Click);
            _nextButton = MakeButton("下一步", NextButton_Click);
            _playButton = MakeButton("播放", PlayButton_Click);

            _prevButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            _nextButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            _playButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            Label speedLabel = new Label();
            speedLabel.Text = "速度";
            speedLabel.AutoSize = false;
            speedLabel.Width = 46;
            speedLabel.Height = 30;
            speedLabel.TextAlign = ContentAlignment.MiddleLeft;
            speedLabel.Margin = new Padding(10, 0, 0, 0);

            _speedTrack = new TrackBar();
            _speedTrack.Minimum = 1;
            _speedTrack.Maximum = 10;
            _speedTrack.Value = 5;
            _speedTrack.MinimumSize = new Size(120, 30);
            _speedTrack.Height = 30;
            _speedTrack.Dock = DockStyle.Fill;
            _speedTrack.Margin = new Padding(4, 0, 0, 0);
            _speedTrack.TickStyle = TickStyle.None;
            _speedTrack.ValueChanged += delegate { UpdateTimerInterval(); };

            playback.Controls.Add(_prevButton, 0, 0);
            playback.Controls.Add(_nextButton, 1, 0);
            playback.Controls.Add(_playButton, 2, 0);
            playback.Controls.Add(speedLabel, 3, 0);
            playback.Controls.Add(_speedTrack, 4, 0);

            side.Controls.Add(playback, 0, 2);

            TabControl bottomTabs = new TabControl();
            bottomTabs.Dock = DockStyle.Fill;
            _rightSplit.Panel2.Controls.Add(bottomTabs);

            TabPage resultPage = new TabPage("评测结果");
            TableLayoutPanel resultLayout = new TableLayoutPanel();
            resultLayout.Dock = DockStyle.Fill;
            resultLayout.RowCount = 2;
            resultLayout.ColumnCount = 1;
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            resultPage.Controls.Add(resultLayout);

            _scoreSummaryLabel = new Label();
            _scoreSummaryLabel.Dock = DockStyle.Fill;
            _scoreSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            _scoreSummaryLabel.Padding = new Padding(10, 4, 10, 4);
            _scoreSummaryLabel.BackColor = Color.FromArgb(245, 247, 250);
            _scoreSummaryLabel.BorderStyle = BorderStyle.FixedSingle;
            resultLayout.Controls.Add(_scoreSummaryLabel, 0, 0);

            _resultGrid = MakeGrid();
            _resultGrid.Columns.Add("Index", "#");
            _resultGrid.Columns.Add("Case", "用例");
            _resultGrid.Columns.Add("N", "N");
            _resultGrid.Columns.Add("Weight", "权重");
            _resultGrid.Columns.Add("Weighted", "加权分");
            _resultGrid.Columns.Add("Status", "状态");
            _resultGrid.Columns.Add("Score", "得分");
            _resultGrid.Columns.Add("Steps", "步数");
            _resultGrid.Columns.Add("Message", "中文提示");
            _resultGrid.Columns.Add("Elapsed", "学生用时(ms)");
            _resultGrid.Columns[0].Width = 46;
            _resultGrid.Columns[1].Width = 160;
            _resultGrid.Columns[8].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _resultGrid.Columns[9].Width = 220;
            _resultGrid.SelectionChanged += ResultGrid_SelectionChanged;
            resultLayout.Controls.Add(_resultGrid, 0, 1);
            bottomTabs.TabPages.Add(resultPage);

            TabPage logPage = new TabPage("OJ 交互");
            _interactionBox = MakeReadOnlyTextBox();
            _interactionBox.Font = new Font("Consolas", 9.5f);
            logPage.Controls.Add(_interactionBox);
            bottomTabs.TabPages.Add(logPage);

            TabPage stepPage = new TabPage("地图快照");
            _stepGrid = MakeGrid();
            _stepGrid.Columns.Add("Step", "步数");
            _stepGrid.Columns.Add("Score", "分数");
            _stepGrid.Columns.Add("Head", "蛇头");
            _stepGrid.Columns.Add("Food", "食物");
            _stepGrid.Columns.Add("Move", "程序指令");
            _stepGrid.Columns.Add("Response", "OJ 返回");
            _stepGrid.Columns.Add("Length", "长度");
            _stepGrid.Columns.Add("Note", "说明");
            _stepGrid.Columns[7].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _stepGrid.SelectionChanged += StepGrid_SelectionChanged;
            stepPage.Controls.Add(_stepGrid);
            bottomTabs.TabPages.Add(stepPage);

            TabPage allCasesPage = new TabPage("测试用例");
            _caseList = new ListView();
            _caseList.Dock = DockStyle.Fill;
            _caseList.View = View.Details;
            _caseList.FullRowSelect = true;
            _caseList.GridLines = true;
            _caseList.Columns.Add("#", 42);
            _caseList.Columns.Add("名称", 180);
            _caseList.Columns.Add("类型", 80);
            _caseList.Columns.Add("N", 60);
            _caseList.Columns.Add("Seed", 90);
            _caseList.Columns.Add("说明", 520);
            allCasesPage.Controls.Add(_caseList);
            bottomTabs.TabPages.Add(allCasesPage);

            TabPage advancedPage = new TabPage("高级设置");
            advancedPage.Controls.Add(BuildAdvancedPanel());
            bottomTabs.TabPages.Add(advancedPage);

            TabPage stderrPage = new TabPage("程序错误输出");
            _stderrBox = MakeReadOnlyTextBox();
            _stderrBox.Font = new Font("Consolas", 9.5f);
            stderrPage.Controls.Add(_stderrBox);
            bottomTabs.TabPages.Add(stderrPage);

            TabPage helpPage = new TabPage("说明");
            _helpBox = MakeReadOnlyTextBox();
            _helpBox.Text = HelpText();
            helpPage.Controls.Add(_helpBox);
            bottomTabs.TabPages.Add(helpPage);

            StatusStrip status = new StatusStrip();
            status.AutoSize = false;
            status.Height = 34;
            status.SizingGrip = false;

            _statusLabel = new ToolStripStatusLabel();
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.AutoToolTip = true;
            _statusLabel.Font = new Font("Microsoft YaHei UI", 9f);

            status.Items.Add(_statusLabel);
            root.Controls.Add(status, 0, 2);

            _playTimer = new System.Windows.Forms.Timer();
            _playTimer.Tick += PlayTimer_Tick;
            UpdateTimerInterval();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(ApplyStartupLayout));
        }

        private void ApplyStartupLayout()
        {
            if (_startupLayoutApplied)
            {
                return;
            }

            _startupLayoutApplied = true;
            SuspendLayout();
            try
            {
                ConfigureSplitter(_mainSplit, 420, 620, (int)(_mainSplit.Width * 0.31));
                ConfigureSplitter(_leftSplit, 320, 120, (int)(_leftSplit.Height * 0.72));
                ConfigureSplitter(_rightSplit, 360, 220, (int)(_rightSplit.Height * 0.57));
                ConfigureSplitter(_mapSplit, 360, 280, (int)(_mapSplit.Width * 0.55));
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private static void ConfigureSplitter(SplitContainer split, int panel1Min, int panel2Min, int desired)
        {
            if (split == null)
            {
                return;
            }

            int size = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            int max = size - split.SplitterWidth - panel2Min;
            if (size <= 0 || max < panel1Min)
            {
                return;
            }

            int value = Math.Min(Math.Max(desired, panel1Min), max);
            if (value > 0 && value < size)
            {
                split.Panel1MinSize = 25;
                split.Panel2MinSize = 25;
                split.SplitterDistance = value;
                split.Panel1MinSize = panel1Min;
                split.Panel2MinSize = panel2Min;
                split.SplitterDistance = value;
            }
        }

        private Control BuildAdvancedPanel()
        {
            Panel outer = new Panel();
            outer.Dock = DockStyle.Fill;
            outer.AutoScroll = true;
            outer.Padding = new Padding(0);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Top;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Padding = new Padding(16, 14, 16, 14);
            panel.RowCount = 6;
            panel.ColumnCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _timeLimitBox = new NumericUpDown();
            _timeLimitBox.Minimum = 50;
            _timeLimitBox.Maximum = 30000;
            _timeLimitBox.Increment = 50;
            _timeLimitBox.Value = 1000;

            _memoryLimitBox = new NumericUpDown();
            _memoryLimitBox.Minimum = 1;
            _memoryLimitBox.Maximum = 4096;
            _memoryLimitBox.Increment = 1;
            _memoryLimitBox.Value = 64;

            _codeLengthLimitBox = new NumericUpDown();
            _codeLengthLimitBox.Minimum = 1;
            _codeLengthLimitBox.Maximum = 1024;
            _codeLengthLimitBox.Increment = 1;
            _codeLengthLimitBox.Value = 32;

            _stackLimitBox = new NumericUpDown();
            _stackLimitBox.Minimum = 64;
            _stackLimitBox.Maximum = 1024 * 1024;
            _stackLimitBox.Increment = 64;
            _stackLimitBox.Value = 8192;

            _lineTimeoutBox = new NumericUpDown();
            _lineTimeoutBox.Minimum = 50;
            _lineTimeoutBox.Maximum = 30000;
            _lineTimeoutBox.Value = 1000;
            _lineTimeoutBox.Visible = false;

            _finalTimeoutBox = new NumericUpDown();
            _finalTimeoutBox.Minimum = 50;
            _finalTimeoutBox.Maximum = 30000;
            _finalTimeoutBox.Value = 1000;
            _finalTimeoutBox.Visible = false;

            AddAdvancedRow(panel, 0, "时间限制(ms)", _timeLimitBox, "默认 1000ms。超过即判 TLE；默认最多继续观察到 3000ms 用于显示诊断用时。");
            AddAdvancedRow(panel, 1, "内存限制(MB)", _memoryLimitBox, "默认 64MB。运行时监控学生程序私有内存，超过即停止。");
            AddAdvancedRow(panel, 2, "代码长度(KB)", _codeLengthLimitBox, "默认 32KB。编译前按 UTF-8 字节数检查，超过会在编译前拦截。");
            AddAdvancedRow(panel, 3, "栈限制(KB)", _stackLimitBox, "默认 8192KB。编译时通过链接器设置 Windows 程序栈大小。");

            TextBox note = MakeReadOnlyTextBox();
            note.Text = "限制说明：本工具不再使用步数上限。每个用例默认按时间 1000ms、内存 64MB、代码长度 32KB、栈 8192KB 限制；超过时间限制后仍判 TLE，但会最多继续观察到 3000ms，若仍未结束会强制停止。食物生成采用固定 seed 的 C 风格伪随机规则，尽量贴近学校 OJ 的交互形态。";
            note.Height = 100;
            note.MinimumSize = new Size(0, 96);
            panel.Controls.Add(note, 0, 4);
            panel.SetColumnSpan(note, 3);

            outer.Controls.Add(panel);
            return outer;
        }

        private void AddAdvancedRow(TableLayoutPanel panel, int row, string label, Control editor, string help)
        {
            Label l = new Label();
            l.Text = label;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Dock = DockStyle.Fill;
            l.Font = new Font("Microsoft YaHei UI", 9f);
            panel.Controls.Add(l, 0, row);

            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 3, 10, 3);
            panel.Controls.Add(editor, 1, row);

            Label h = new Label();
            h.Text = help;
            h.TextAlign = ContentAlignment.MiddleLeft;
            h.Dock = DockStyle.Fill;
            h.AutoEllipsis = true;
            h.Font = new Font("Microsoft YaHei UI", 9f);
            panel.Controls.Add(h, 2, row);
        }

        private Button MakeButton(string text, EventHandler handler)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.Height = 28;
            b.Margin = new Padding(4, 0, 4, 0);
            b.Click += handler;
            return b;
        }

        private Button MakeModeButton(string text, bool scoreMode)
        {
            Button b = new Button();
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.MinimumSize = new Size(0, 36);
            b.Margin = new Padding(0, 2, 0, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.AutoEllipsis = false;
            b.Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold);
            b.Tag = scoreMode;
            b.Click += ModeSegment_Click;
            return b;
        }

        private TextBox MakeReadOnlyTextBox()
        {
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.ReadOnly = true;
            box.Dock = DockStyle.Fill;
            box.BackColor = Color.White;
            return box;
        }

        private DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.RowTemplate.Height = 26;
            grid.BackgroundColor = Color.White;
            return grid;
        }

        private void LoadCases()
        {
            _caseCombo.Items.Clear();
            _caseList.Items.Clear();
            for (int i = 0; i < _cases.Count; i++)
            {
                TestCase tc = _cases[i];
                _caseCombo.Items.Add((i + 1).ToString("00") + "  " + tc.Name);
                ListViewItem item = new ListViewItem((i + 1).ToString());
                item.SubItems.Add(tc.Name);
                item.SubItems.Add(tc.IsScoringCase ? "跑分" : "调试");
                item.SubItems.Add(tc.N.ToString());
                item.SubItems.Add(tc.Seed.ToString());
                item.SubItems.Add(tc.Description);
                _caseList.Items.Add(item);
            }
            if (_caseCombo.Items.Count > 0)
            {
                _caseCombo.SelectedIndex = 0;
            }
        }

        private void ShowSelectedCaseInfo()
        {
            int index = _caseCombo.SelectedIndex;
            if (index < 0 || index >= _cases.Count)
            {
                return;
            }

            TestCase tc = _cases[index];
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("用例：" + tc.Name);
            sb.AppendLine("类型：" + (tc.IsScoringCase ? "跑分用例" : "调试用例"));
            sb.AppendLine("N：" + tc.N);
            sb.AppendLine("Seed：" + tc.Seed);
            sb.AppendLine("限制：时间 1000ms，内存 64MB，代码长度 32KB，栈 8192KB；无步数上限。");
            sb.AppendLine("说明：" + tc.Description);
            sb.AppendLine();
            for (int i = 0; i < tc.InitialMap.Length; i++)
            {
                sb.AppendLine(tc.InitialMap[i]);
            }
            sb.AppendLine(tc.N.ToString());
            _caseInfoBox.Text = sb.ToString();
        }


        private void ShowInitialPreview()
        {
            if (_caseCombo == null || _mapView == null || _variableList == null || _stepGrid == null)
            {
                return;
            }

            int index = _caseCombo.SelectedIndex;
            if (index < 0 || index >= _cases.Count)
            {
                return;
            }

            TestCase tc = _cases[index];
            GridSnapshot snapshot = CreateInitialSnapshot(tc);

            _currentResult = null;
            _currentStepIndex = 0;
            _mapView.Snapshot = snapshot;

            if (_stepLabel != null)
            {
                _stepLabel.Text = "初始地图：" + tc.Name;
            }

            _stepGrid.Rows.Clear();
            _stepGrid.Rows.Add(
                "0",
                "0",
                Pos(snapshot.Head),
                Pos(snapshot.Food),
                "-",
                "-",
                snapshot.SnakeLength.ToString(),
                "当前用例初始状态，尚未运行学生程序。"
            );

            _variableList.Items.Clear();
            AddVariable("用例", tc.Name);
            AddVariable("N", tc.N.ToString());
            AddVariable("蛇长", snapshot.SnakeLength.ToString());
            AddVariable("蛇头", Pos(snapshot.Head));
            AddVariable("食物", Pos(snapshot.Food));
            AddVariable("当前方向", DirectionHelper.ChineseName(snapshot.CurrentDirection));
            AddVariable("程序指令", "-");
            AddVariable("OJ 返回", "-");
            AddVariable("吃到食物", "否");
            AddVariable("本步增长", "否");
            AddVariable("是否结束", "否");
            AddVariable("说明", "当前用例初始状态，尚未运行学生程序。");
        }

        private GridSnapshot CreateInitialSnapshot(TestCase tc)
        {
            GridSnapshot snapshot = new GridSnapshot();
            snapshot.CaseName = tc.Name;
            snapshot.Step = 0;
            snapshot.Score = 0;
            snapshot.N = tc.N;
            snapshot.SnakeLength = 0;
            snapshot.Head = new Point(-1, -1);
            snapshot.Food = new Point(-1, -1);
            snapshot.CurrentDirection = Direction.Up;
            snapshot.StudentMove = "";
            snapshot.OjResponse = "";
            snapshot.AteFood = false;
            snapshot.Grew = false;
            snapshot.Ended = false;
            snapshot.EndReason = "";
            snapshot.Note = "当前用例初始状态。";

            char[,] map = new char[20, 20];

            for (int r = 0; r < 20; r++)
            {
                for (int c = 0; c < 20; c++)
                {
                    char ch = tc.InitialMap[r][c];
                    map[r, c] = ch;

                    if (ch == 'H')
                    {
                        snapshot.Head = new Point(c, r);
                        snapshot.SnakeLength++;
                    }
                    else if (ch == 'B')
                    {
                        snapshot.SnakeLength++;
                    }
                    else if (ch == 'F')
                    {
                        snapshot.Food = new Point(c, r);
                    }
                }
            }

            snapshot.Map = map;
            return snapshot;
        }

        private void RefreshGccInfo()
        {
            string gcc = JudgeEngine.FindGcc();
            _gccLabel.Text = string.IsNullOrEmpty(gcc) ? "GCC：未检测到" : "GCC：已检测到";
            _compileBox.Text = JudgeEngine.GetGccVersion(gcc);
        }

        private void ModeSegment_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null || button.Tag == null)
            {
                return;
            }

            bool nextMode = (bool)button.Tag;
            if (_scoreMode == nextMode)
            {
                return;
            }

            _scoreMode = nextMode;
            _lastRunWasScoring = false;
            UpdateModeUi();
            UpdateScoreSummary();
        }

        private void ScoringGroupCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            _scoreGroupDirtySinceRun = _lastRunWasScoring
                && _results.Count > 0
                && UseRandomScoringGroup() != _lastRunUsedRandomScoringGroup;
            UpdateModeUi();
            UpdateScoreSummary();
        }

        private void UpdateModeUi()
        {
            if (_debugModeButton == null || _scoreModeButton == null)
            {
                return;
            }

            if (_scoreGroupHintLabel != null)
            {
                _scoreGroupHintLabel.Visible = _scoreMode;
                _scoreGroupHintLabel.Enabled = !_busy;
            }
            if (_scoreGroupCombo != null)
            {
                _scoreGroupCombo.Visible = _scoreMode;
                _scoreGroupCombo.Enabled = _scoreMode && !_busy;
            }

            if (_scoreMode)
            {
                ApplyModeButtonStyle(_debugModeButton, false, false);
                ApplyModeButtonStyle(_scoreModeButton, true, true);
                _modeHintLabel.Text = "当前模式：跑分评分";
                _batchButton.Text = "开始跑分(10组)";
                _runButton.Text = "调试当前用例";
                _caseCombo.BackColor = Color.FromArgb(239, 246, 255);
                UpdateBusyState(_busy, "当前为跑分评分：测试组 " + CurrentScoringGroupLabel() + "，顺序运行 10 组权重用例并计算基础分。左侧可切回 Debug 调试。");
            }
            else
            {
                ApplyModeButtonStyle(_debugModeButton, true, false);
                ApplyModeButtonStyle(_scoreModeButton, false, true);
                _modeHintLabel.Text = "当前模式：Debug 调试";
                _batchButton.Text = "批量评测";
                _runButton.Text = "运行当前用例";
                _caseCombo.BackColor = SystemColors.Window;
                UpdateBusyState(_busy, "当前为 Debug 调试：查看地图、变量、交互日志。右侧可切到跑分评分。");
            }
        }

        private void ApplyModeButtonStyle(Button button, bool active, bool scoreButton)
        {
            if (active)
            {
                button.BackColor = scoreButton ? Color.FromArgb(30, 120, 210) : Color.FromArgb(32, 144, 98);
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = button.BackColor;
            }
            else
            {
                button.BackColor = Color.White;
                button.ForeColor = Color.FromArgb(45, 55, 72);
                button.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 204);
            }
        }

        private RunOptions CollectOptions()
        {
            RunOptions options = new RunOptions();
            options.TimeLimitMs = (int)_timeLimitBox.Value;
            options.LineTimeoutMs = options.TimeLimitMs;
            options.FinalTimeoutMs = options.TimeLimitMs;
            options.TimeoutObservationMs = 3000;
            options.MemoryLimitKb = (int)_memoryLimitBox.Value * 1024;
            options.CodeLengthLimitKb = (int)_codeLengthLimitBox.Value;
            options.StackLimitKb = (int)_stackLimitBox.Value;
            return options;
        }

        private void CodeBox_TextChanged(object sender, EventArgs e)
        {
            if (_results.Count > 0)
            {
                _codeDirtySinceRun = true;
                UpdateScoreSummary();
            }
        }

        private void DetectGccButton_Click(object sender, EventArgs e)
        {
            RefreshGccInfo();
            MessageBox.Show(this, _compileBox.Text, "GCC 检测", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CompileButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            UpdateBusyState(true, "正在编译...");
            string code = _codeBox.Text;
            RunOptions options = CollectOptions();
            Task.Factory.StartNew(delegate
            {
                return JudgeEngine.Compile(code, _workDir, options);
            }).ContinueWith(delegate(Task<CompileResult> task)
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        _compileBox.Text = "编译任务异常：" + TaskFailureMessage(task);
                        UpdateBusyState(false, "编译异常");
                        return;
                    }
                    CompileResult cr = task.Result;
                    _compileBox.Text = cr.Message + Environment.NewLine + Environment.NewLine + cr.CompilerOutput;
                    UpdateBusyState(false, cr.Success ? "编译成功" : "编译失败");
                }));
            });
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            int index = _caseCombo.SelectedIndex;
            if (index < 0 || index >= _cases.Count)
            {
                MessageBox.Show(this, "请先选择一个测试用例。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StartRun(false);
        }

        private void BatchButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            StartRun(true);
        }

        private void StartRun(bool batch)
        {
            bool scoringRun = batch && _scoreMode;
            string scoringGroupLabel;
            bool usedRandomScoringGroup;
            int selectedIndex = _caseCombo.SelectedIndex;
            List<TestCase> runCases = BuildRunCases(batch, selectedIndex, out scoringGroupLabel, out usedRandomScoringGroup);
            UpdateBusyState(true, scoringRun ? "正在按文档规则跑分：" + scoringGroupLabel + "..." : (batch ? "正在批量评测..." : "正在运行当前用例..."));
            string code = _codeBox.Text;
            RunOptions options = CollectOptions();
            if (scoringRun)
            {
                options.CaptureDetails = true;
                options.MaxSnapshotsToKeep = ScoringSnapshotLimit;
                options.MaxLogChars = ScoringMaxLogChars;
                options.LimitCheckIntervalMs = 25;
                options.SnapshotInterval = 1;
            }
            BeginEvaluationUi(scoringRun, batch, code, runCases.Count, scoringGroupLabel);

            Task.Factory.StartNew(delegate
            {
                RunPayload payload = new RunPayload();
                payload.ScoringRun = scoringRun;
                payload.ScoringGroupLabel = scoringGroupLabel;
                payload.UsedRandomScoringGroup = usedRandomScoringGroup;
                payload.CodeFingerprint = CodeFingerprint(code);
                payload.RunTime = DateTime.Now;
                payload.Compile = JudgeEngine.Compile(code, _workDir, options);
                if (!payload.Compile.Success)
                {
                    return payload;
                }

                for (int i = 0; i < runCases.Count; i++)
                {
                    JudgeResult caseResult = JudgeEngine.RunCase(payload.Compile.ExePath, runCases[i], options);
                    caseResult.CaseIndex = i;
                    payload.Results.Add(caseResult);
                    ReportRunProgress(scoringRun, i + 1, runCases.Count, caseResult);
                }
                return payload;
            }).ContinueWith(delegate(Task<RunPayload> task)
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        _compileBox.Text = "评测任务异常：" + TaskFailureMessage(task);
                        _scoreSummaryLabel.Text = "评测器后台任务异常，当前结果未完成。请查看编译/运行信息后重试。";
                        _scoreSummaryLabel.BackColor = Color.FromArgb(255, 247, 237);
                        UpdateBusyState(false, "评测异常");
                        return;
                    }
                    ApplyRunPayload(task.Result, batch);
                    UpdateBusyState(false, "运行完成");
                }));
            });
        }

        private void BeginEvaluationUi(bool scoringRun, bool batch, string code, int caseCount, string scoringGroupLabel)
        {
            if (_playTimer != null)
            {
                _playTimer.Enabled = false;
                _playButton.Text = "播放";
            }

            _results.Clear();
            _resultGrid.Rows.Clear();
            _stepGrid.Rows.Clear();
            _interactionBox.Text = "";
            _stderrBox.Text = "";
            _variableList.Items.Clear();
            _currentResult = null;
            _currentStepIndex = 0;
            _mapView.Snapshot = null;

            _lastRunWasScoring = scoringRun;
            _codeDirtySinceRun = false;
            _scoreGroupDirtySinceRun = false;
            _lastRunCodeFingerprint = CodeFingerprint(code);
            _lastRunExePath = "";
            _lastRunTime = DateTime.Now;
            _lastProgressDone = 0;

            if (scoringRun)
            {
                _scoreSummaryLabel.Text = "正在重新跑分：本次会重新编译当前代码，并运行 " + caseCount + " 个跑分用例。测试组：" + scoringGroupLabel + "。代码指纹：" + _lastRunCodeFingerprint + "。";
                _scoreSummaryLabel.Text += " 跑分用例会按顺序逐个执行；结果表和导出日志会显示每个样例的学生程序用时；每个用例保留最多 " + ScoringSnapshotLimit + " 个逐步地图快照，超出时保留开头和末尾关键段。";
                _scoreSummaryLabel.BackColor = Color.FromArgb(235, 247, 255);
            }
            else if (batch)
            {
                _scoreSummaryLabel.Text = "正在批量评测：本次会重新编译当前代码，并运行 " + caseCount + " 个调试用例。代码指纹：" + _lastRunCodeFingerprint + "。";
                _scoreSummaryLabel.BackColor = Color.FromArgb(245, 247, 250);
            }
            else
            {
                _scoreSummaryLabel.Text = "正在运行当前用例：本次会重新编译当前代码。代码指纹：" + _lastRunCodeFingerprint + "。";
                _scoreSummaryLabel.BackColor = Color.FromArgb(245, 247, 250);
            }
        }

        private List<TestCase> BuildRunCases(bool batch, int selectedIndex, out string scoringGroupLabel, out bool usedRandomScoringGroup)
        {
            List<TestCase> cases = new List<TestCase>();
            scoringGroupLabel = "";
            usedRandomScoringGroup = false;
            if (!batch)
            {
                cases.Add(_cases[selectedIndex]);
                return cases;
            }

            if (_scoreMode)
            {
                scoringGroupLabel = CurrentScoringGroupLabel();
                if (UseRandomScoringGroup())
                {
                    usedRandomScoringGroup = true;
                    int groupSeed = CreateRandomScoringGroupSeed();
                    scoringGroupLabel = "随机组（组种子 " + groupSeed + "）";
                    cases.AddRange(TestCaseFactory.CreateRandomScoringCases(groupSeed));
                    return cases;
                }

                for (int i = 0; i < _cases.Count; i++)
                {
                    if (_cases[i].IsScoringCase)
                    {
                        cases.Add(_cases[i]);
                    }
                }
                return cases;
            }

            cases.AddRange(_cases);
            return cases;
        }

        private void ReportRunProgress(bool scoringRun, int done, int total, JudgeResult result)
        {
            try
            {
                if (!IsHandleCreated)
                {
                    return;
                }
                if (done <= _lastProgressDone && done < total)
                {
                    return;
                }
                _lastProgressDone = done;

                BeginInvoke(new MethodInvoker(delegate
                {
                    string prefix = scoringRun ? "跑分中" : "评测中";
                    string text = prefix + "：" + done + "/" + total + "，刚完成 " + result.CaseName + "，状态 " + result.StatusText + "，得分 " + result.Score + "，学生用时 " + DisplayElapsed(result) + "。";
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text = text;
                    }
                    if (_scoreSummaryLabel != null)
                    {
                        _scoreSummaryLabel.Text = text + (scoringRun ? " 正在保留逐步图形快照。": "");
                    }
                }));
            }
            catch
            {
            }
        }

        private void ApplyRunPayload(RunPayload payload, bool batch)
        {
            _lastRunWasScoring = payload.ScoringRun;
            if (payload.ScoringRun)
            {
                _lastScoringGroupLabel = payload.ScoringGroupLabel;
                _lastRunUsedRandomScoringGroup = payload.UsedRandomScoringGroup;
                _scoreGroupDirtySinceRun = UseRandomScoringGroup() != _lastRunUsedRandomScoringGroup;
            }
            _compileBox.Text = payload.Compile.Message + Environment.NewLine + Environment.NewLine + payload.Compile.CompilerOutput;
            if (!payload.Compile.Success)
            {
                _lastRunCodeFingerprint = string.IsNullOrEmpty(payload.Compile.SourceFingerprint) ? payload.CodeFingerprint : payload.Compile.SourceFingerprint;
                _lastRunExePath = payload.Compile.ExePath;
                _lastRunTime = payload.RunTime;
                _codeDirtySinceRun = CodeFingerprint(_codeBox.Text) != _lastRunCodeFingerprint;
                _results.Clear();
                _resultGrid.Rows.Clear();
                UpdateScoreSummary();
                _interactionBox.Text = "";
                _stderrBox.Text = "";
                _stepGrid.Rows.Clear();
                _currentResult = null;
                _mapView.Snapshot = null;
                return;
            }

            _results.Clear();
            _results.AddRange(payload.Results);
            _lastRunCodeFingerprint = string.IsNullOrEmpty(payload.Compile.SourceFingerprint) ? payload.CodeFingerprint : payload.Compile.SourceFingerprint;
            _lastRunExePath = payload.Compile.ExePath;
            _lastRunTime = payload.RunTime;
            _codeDirtySinceRun = CodeFingerprint(_codeBox.Text) != _lastRunCodeFingerprint;
            FillResultGrid();
            UpdateScoreSummary();
            if (_results.Count > 0)
            {
                _resultGrid.Rows[0].Selected = true;
                LoadResult(_results[0]);
            }

            if (batch)
            {
                int total = 0;
                for (int i = 0; i < _results.Count; i++) total += _results[i].Score;
                if (payload.ScoringRun)
                {
                    _compileBox.Text += Environment.NewLine + BuildScoreSummaryText();
                }
                else
                {
                    _compileBox.Text += Environment.NewLine + "批量评测完成，当前本地总原始分：" + total + "。";
                }
            }
        }

        private string DisplayElapsed(JudgeResult result)
        {
            if (result == null)
            {
                return "";
            }

            string elapsed = "";
            if (result.ProgramElapsedMs >= 0)
            {
                elapsed = result.ProgramElapsedMs.ToString();
            }
            else if (result.ElapsedMs >= 0)
            {
                elapsed = result.ElapsedMs.ToString();
            }

            if (result.Status == JudgeStatus.TimeLimitExceeded && result.TimeLimitExceededAtMs > 0)
            {
                if (result.StoppedAtDiagnosticLimit)
                {
                    return elapsed + "（超时点 " + result.TimeLimitExceededAtMs + "，已到 " + result.DiagnosticLimitMs + "ms 上限）";
                }
                return elapsed + "（超时点 " + result.TimeLimitExceededAtMs + "）";
            }

            return elapsed;
        }

        private static string TaskFailureMessage(Task task)
        {
            if (task == null)
            {
                return "未知错误。";
            }
            if (task.IsCanceled)
            {
                return "任务已取消。";
            }
            if (task.Exception != null)
            {
                Exception ex = task.Exception.GetBaseException();
                if (ex != null)
                {
                    return ex.Message;
                }
            }
            return "未知错误。";
        }

        private void FillResultGrid()
        {
            _resultGrid.SuspendLayout();
            _resultGrid.Rows.Clear();
            try
            {
                for (int i = 0; i < _results.Count; i++)
                {
                    JudgeResult r = _results[i];
                    double weight = WeightForN(r.N);
                    double weighted = r.Score * weight;
                    int row = _resultGrid.Rows.Add((i + 1).ToString(), r.CaseName, r.N.ToString(), weight.ToString("0.###"), weighted.ToString("0.##"), r.StatusText, r.Score.ToString(), r.Steps.ToString(), r.Message, DisplayElapsed(r));
                    DataGridViewRow gridRow = _resultGrid.Rows[row];
                    if (r.Status == JudgeStatus.Accepted)
                    {
                        gridRow.DefaultCellStyle.BackColor = Color.FromArgb(236, 253, 245);
                    }
                    else
                    {
                        gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 241, 242);
                    }
                }
            }
            finally
            {
                _resultGrid.ResumeLayout();
            }
        }

        private void UpdateScoreSummary()
        {
            if (_scoreSummaryLabel == null)
            {
                return;
            }

            if (_results.Count == 0 && !string.IsNullOrEmpty(_lastRunCodeFingerprint) && _codeDirtySinceRun)
            {
                _scoreSummaryLabel.Text = "当前代码已经修改，尚未对当前代码运行测评。请点击“运行当前用例”“批量评测”或“开始跑分(10组)”。";
                _scoreSummaryLabel.BackColor = Color.FromArgb(255, 247, 237);
            }
            else if (_results.Count > 0 && (_codeDirtySinceRun || (_lastRunWasScoring && _scoreGroupDirtySinceRun)))
            {
                string rerunText = _lastRunWasScoring ? "请点击“开始跑分(10组)”重新计算。" : "请重新点击测评按钮。";
                string oldSummary = _lastRunWasScoring ? BuildScoreSummaryText() : BuildDebugSummaryText();
                string staleReason = _codeDirtySinceRun ? "当前代码已经修改" : "当前跑分测试组已经切换";
                _scoreSummaryLabel.Text = staleReason + "，下面显示的是上一次测评结果，并不是当前配置的结果。" + rerunText
                    + Environment.NewLine + oldSummary;
                _scoreSummaryLabel.BackColor = Color.FromArgb(255, 247, 237);
            }
            else if (_lastRunWasScoring && _results.Count > 0)
            {
                _scoreSummaryLabel.Text = BuildScoreSummaryText();
                _scoreSummaryLabel.BackColor = Color.FromArgb(235, 247, 255);
            }
            else if (_results.Count == 0 && _scoreMode)
            {
                _scoreSummaryLabel.Text = "跑分模式：点击“开始跑分(10组)”，按当前测试组 " + CurrentScoringGroupLabel() + " 顺序运行 10 个 N 权重用例；总分 = Σ(原始分 / (log2(N) + 1))，然后按基础分规则折算，忽略排名奖励；结果表会显示每个样例的学生程序用时。";
                _scoreSummaryLabel.BackColor = Color.FromArgb(235, 247, 255);
            }
            else if (_results.Count > 0)
            {
                _scoreSummaryLabel.Text = BuildDebugSummaryText();
                _scoreSummaryLabel.BackColor = Color.FromArgb(245, 247, 250);
            }
            else
            {
                _scoreSummaryLabel.Text = "Debug模式：结果表用于定位编译、交互、地图和状态问题；批量评测会运行全部内置测试用例，不按文档基础分折算。";
                _scoreSummaryLabel.BackColor = Color.FromArgb(245, 247, 250);
            }
        }

        private string BuildScoreSummaryText()
        {
            double total = 0.0;
            int rawTotal = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                JudgeResult r = _results[i];
                rawTotal += r.Score;
                total += r.Score * WeightForN(r.N);
            }

            int basic = BasicScore(total);
            string summary = "跑分结果：" + _results.Count + " 个样例各运行 1 次，原始分合计 " + rawTotal
                + "，加权总分 " + total.ToString("0.##")
                + "，基础分 " + basic + "/50。排名奖励分已按要求忽略。";
            summary = "测试组：" + _lastScoringGroupLabel + "  " + summary;
            if (!string.IsNullOrEmpty(_lastRunCodeFingerprint))
            {
                summary += "  本次代码指纹：" + _lastRunCodeFingerprint + "，运行时间：" + _lastRunTime.ToString("HH:mm:ss") + "。";
            }
            if (!string.IsNullOrEmpty(_lastRunExePath))
            {
                summary += "  运行程序：" + _lastRunExePath;
            }
            return summary;
        }

        private string BuildDebugSummaryText()
        {
            int rawTotal = 0;
            int passed = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                rawTotal += _results[i].Score;
                if (_results[i].Status == JudgeStatus.Accepted)
                {
                    passed++;
                }
            }

            string summary = "调试评测结果：" + _results.Count + " 个用例，原始分合计 " + rawTotal + "，通过/存活 " + passed + " 个。";
            if (!string.IsNullOrEmpty(_lastRunCodeFingerprint))
            {
                summary += "  本次代码指纹：" + _lastRunCodeFingerprint + "，运行时间：" + _lastRunTime.ToString("HH:mm:ss") + "。";
            }
            if (!string.IsNullOrEmpty(_lastRunExePath))
            {
                summary += "  运行程序：" + _lastRunExePath;
            }
            return summary;
        }

        private static string CodeFingerprint(string text)
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

        private static double WeightForN(int n)
        {
            if (n <= 0)
            {
                return 0.0;
            }
            return 1.0 / ((Math.Log(n) / Math.Log(2.0)) + 1.0);
        }

        private string CurrentScoringGroupLabel()
        {
            if (_scoreGroupCombo == null || _scoreGroupCombo.SelectedIndex <= 0)
            {
                return FixedScoringGroupLabel;
            }
            return RandomScoringGroupLabel;
        }

        private bool UseRandomScoringGroup()
        {
            return _scoreGroupCombo != null && _scoreGroupCombo.SelectedIndex == 1;
        }

        private static int CreateRandomScoringGroupSeed()
        {
            long ticks = DateTime.UtcNow.Ticks;
            int seed = unchecked((int)(ticks ^ (ticks >> 32)));
            if (seed == int.MinValue)
            {
                seed = 20260510;
            }
            if (seed < 0)
            {
                seed = -seed;
            }
            if (seed == 0)
            {
                seed = 20260510;
            }
            return seed;
        }

        private static int BasicScore(double weightedTotal)
        {
            if (weightedTotal >= 500.0) return 50;
            if (weightedTotal >= 400.0) return 45;
            if (weightedTotal >= 300.0) return 40;
            if (weightedTotal >= 200.0) return 35;
            if (weightedTotal >= 100.0) return 30;
            return 0;
        }

        private void ResultGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (_resultGrid.SelectedRows.Count == 0)
            {
                return;
            }
            int index = _resultGrid.SelectedRows[0].Index;
            if (index >= 0 && index < _results.Count)
            {
                LoadResult(_results[index]);
            }
        }

        private void LoadResult(JudgeResult result)
        {
            _currentResult = result;
            _interactionBox.Text = result.InteractionLog;
            _stderrBox.Text = string.IsNullOrEmpty(result.ProgramError) ? "无 stderr 输出。" : result.ProgramError;
            if (_lastRunWasScoring)
            {
                _interactionBox.Text = "跑分模式已保留逐步地图快照，可在“地图快照”页用上一步、下一步和播放查看；若步数超过 " + ScoringSnapshotLimit + "，会保留开头和末尾关键段以保持界面流畅。" + Environment.NewLine + Environment.NewLine + _interactionBox.Text;
            }
            _stepGrid.SuspendLayout();
            _stepGrid.Rows.Clear();
            try
            {
                for (int i = 0; i < result.Snapshots.Count; i++)
                {
                    GridSnapshot s = result.Snapshots[i];
                    _stepGrid.Rows.Add(s.Step.ToString(), s.Score.ToString(), Pos(s.Head), Pos(s.Food), s.StudentMove, s.OjResponse, s.SnakeLength.ToString(), s.Ended ? s.EndReason : s.Note);
                }
            }
            finally
            {
                _stepGrid.ResumeLayout();
            }

            if (result.Snapshots.Count > 0)
            {
                ShowSnapshot(result.Snapshots.Count - 1);
                int last = result.Snapshots.Count - 1;
                if (last < _stepGrid.Rows.Count)
                {
                    _stepGrid.Rows[last].Selected = true;
                }
            }
            else
            {
                _mapView.Snapshot = null;
                _variableList.Items.Clear();
                _stepLabel.Text = "快照：无";
            }
        }

        private void StepGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (_currentResult == null || _stepGrid.SelectedRows.Count == 0)
            {
                return;
            }
            int index = _stepGrid.SelectedRows[0].Index;
            if (index >= 0 && index < _currentResult.Snapshots.Count)
            {
                ShowSnapshot(index);
            }
        }

        private void ShowSnapshot(int index)
        {
            if (_currentResult == null || index < 0 || index >= _currentResult.Snapshots.Count)
            {
                return;
            }

            _currentStepIndex = index;
            GridSnapshot s = _currentResult.Snapshots[index];
            _mapView.Snapshot = s;
            _stepLabel.Text = "快照 " + (index + 1) + "/" + _currentResult.Snapshots.Count + "    步数 " + s.Step + "    分数 " + s.Score;
            FillVariables(s);
        }

        private void FillVariables(GridSnapshot s)
        {
            _variableList.Items.Clear();
            AddVariable("用例", s.CaseName);
            AddVariable("步数", s.Step.ToString());
            AddVariable("分数", s.Score.ToString());
            AddVariable("N", s.N.ToString());
            AddVariable("蛇长", s.SnakeLength.ToString());
            AddVariable("蛇头", Pos(s.Head));
            AddVariable("食物", Pos(s.Food));
            AddVariable("当前方向", DirectionHelper.ChineseName(s.CurrentDirection));
            AddVariable("程序指令", EmptyDash(s.StudentMove));
            AddVariable("OJ 返回", EmptyDash(s.OjResponse));
            AddVariable("吃到食物", s.AteFood ? "是" : "否");
            AddVariable("本步增长", s.Grew ? "是" : "否");
            AddVariable("是否结束", s.Ended ? "是" : "否");
            AddVariable("说明", s.Ended ? s.EndReason : s.Note);
        }

        private void AddVariable(string name, string value)
        {
            ListViewItem item = new ListViewItem(name);
            item.SubItems.Add(value == null ? "" : value);
            _variableList.Items.Add(item);
        }

        private void PrevButton_Click(object sender, EventArgs e)
        {
            if (_currentResult == null) return;
            if (_currentStepIndex > 0)
            {
                ShowSnapshot(_currentStepIndex - 1);
                SelectStepRow(_currentStepIndex);
            }
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            if (_currentResult == null) return;
            if (_currentStepIndex + 1 < _currentResult.Snapshots.Count)
            {
                ShowSnapshot(_currentStepIndex + 1);
                SelectStepRow(_currentStepIndex);
            }
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (_currentResult == null || _currentResult.Snapshots.Count == 0)
            {
                return;
            }
            _playTimer.Enabled = !_playTimer.Enabled;
            _playButton.Text = _playTimer.Enabled ? "暂停" : "播放";
        }

        private void PlayTimer_Tick(object sender, EventArgs e)
        {
            if (_currentResult == null)
            {
                _playTimer.Enabled = false;
                _playButton.Text = "播放";
                return;
            }
            if (_currentStepIndex + 1 >= _currentResult.Snapshots.Count)
            {
                _playTimer.Enabled = false;
                _playButton.Text = "播放";
                return;
            }
            ShowSnapshot(_currentStepIndex + 1);
            SelectStepRow(_currentStepIndex);
        }

        private void SelectStepRow(int index)
        {
            if (index >= 0 && index < _stepGrid.Rows.Count)
            {
                _stepGrid.ClearSelection();
                _stepGrid.Rows[index].Selected = true;
            }
        }

        private void UpdateTimerInterval()
        {
            if (_playTimer != null)
            {
                _playTimer.Interval = Math.Max(80, 1150 - _speedTrack.Value * 100);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                MessageBox.Show(this, "评测或编译正在进行。为了避免留下临时源码文件，请等待当前任务完成后再关闭程序。", "正在运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
                return;
            }

            try
            {
                CleanupWorkDirectory();
            }
            catch
            {
            }
            base.OnFormClosing(e);
        }

        private void CleanupWorkDirectory()
        {
            try
            {
                string root = Path.GetFullPath(_rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string work = Path.GetFullPath(_workDir);
                if (!work.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                if (Directory.Exists(work))
                {
                    Directory.Delete(work, true);
                }
                Directory.CreateDirectory(work);
            }
            catch
            {
                // 清理失败时不影响主程序启动；下次启动还会再次尝试清理。
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "C 源码 (*.c)|*.c|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _codeBox.Text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                UpdateBusyState(false, "已加载代码：" + dialog.FileName);
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "C 源码 (*.c)|*.c|所有文件 (*.*)|*.*";
            dialog.FileName = "student.c";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, _codeBox.Text, new UTF8Encoding(false));
                UpdateBusyState(false, "已保存代码：" + dialog.FileName);
            }
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "文本日志 (*.txt)|*.txt|所有文件 (*.*)|*.*";
            dialog.FileName = "SnakeOJTester_评测日志.txt";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, BuildExportLog(), new UTF8Encoding(false));
                UpdateBusyState(false, "已导出日志：" + dialog.FileName);
            }
        }

        private string BuildExportLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("贪吃蛇 OJ 本地测试器日志");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("编译信息");
            sb.AppendLine(_compileBox.Text);
            sb.AppendLine();
            sb.AppendLine("评测结果");
            for (int i = 0; i < _results.Count; i++)
            {
                JudgeResult r = _results[i];
                sb.AppendLine((i + 1) + ". " + r.CaseName + "  " + r.StatusText + "  分数=" + r.Score + "  步数=" + r.Steps + "  学生用时(ms)=" + DisplayElapsed(r));
                sb.AppendLine("   " + r.Message);
            }
            sb.AppendLine();
            for (int i = 0; i < _results.Count; i++)
            {
                JudgeResult r = _results[i];
                sb.AppendLine("===== " + r.CaseName + " / OJ 交互 =====");
                sb.AppendLine(r.InteractionLog);
                if (!string.IsNullOrEmpty(r.ProgramError))
                {
                    sb.AppendLine("===== stderr =====");
                    sb.AppendLine(r.ProgramError);
                }
            }
            return sb.ToString();
        }

        private void UpdateBusyState(bool busy, string status)
        {
            _busy = busy;
            if (_debugModeButton != null) _debugModeButton.Enabled = !busy;
            if (_scoreModeButton != null) _scoreModeButton.Enabled = !busy;
            if (_scoreGroupCombo != null) _scoreGroupCombo.Enabled = _scoreMode && !busy;
            if (_detectGccButton != null) _detectGccButton.Enabled = !busy;
            if (_compileButton != null) _compileButton.Enabled = !busy;
            if (_runButton != null) _runButton.Enabled = !busy;
            if (_batchButton != null) _batchButton.Enabled = !busy;
            if (_loadButton != null) _loadButton.Enabled = !busy;
            if (_saveButton != null) _saveButton.Enabled = !busy;
            if (_exportButton != null) _exportButton.Enabled = !busy;
            if (_codeBox != null) _codeBox.ReadOnly = busy;
            if (_statusLabel != null) _statusLabel.Text = status;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private static string Pos(Point p)
        {
            if (p.X < 0 || p.Y < 0) return "-";
            return "(" + p.Y + ", " + p.X + ")";
        }

        private static string EmptyDash(string text)
        {
            return string.IsNullOrEmpty(text) ? "-" : text;
        }

        private string HelpText()
        {
            return
@"使用流程：
1. 在左侧粘贴完整 C 程序，必须包含 main()。
2. 点击“编译”查看 gcc 报错；没有 gcc 时界面会提示安装 MinGW-w64 或 MSYS2。
3. 点击“运行当前用例”查看地图、变量、OJ 交互输出和中文判定。
4. 点击“批量评测”一次运行全部内置用例，结果表会显示通过、超时、格式错误、答案错误等状态。
5. 点击主界面的大按钮可在 Debug模式 和 跑分模式之间切换。
6. 地图外侧显示 0..19 的行号、列号，方便按题目坐标数格子。
7. 地图快照支持上一步、下一步、播放、暂停和调速。

交互规则：
程序每轮应输出两行：第一行 W/A/S/D，第二行为移动前分数。输出后必须 fflush(stdout)。
OJ 返回：新食物坐标、20 20 表示继续、100 100 表示游戏结束。
收到 100 100 后，程序应输出碰撞前一刻的 20 行地图和第 21 行分数，然后立刻结束。

本地评测说明：
变量状态显示的是评测器内部状态，包括蛇头、食物、方向、分数、N、蛇长、是否增长和结束原因。
食物生成采用固定 seed 的 C 风格伪随机规则，尽量贴近学校 OJ 的交互形态，但隐藏测试的 seed 与地图无法完全等同。
跑分模式只运行 10 个跑分用例，按总分 = Σ(原始分 / (log2(N) + 1)) 计算加权总分，再按文档基础分规则折算，忽略排名奖励分；这 10 个样例会按顺序逐个执行，结果表会显示每个样例的实际用时，并同样支持在“地图快照”页逐步查看图形化回放。
本工具会运行本机 C 程序，请只测试自己信任的代码。

绿色版卸载：
关闭程序后，直接删除 SnakeOJTester 文件夹即可。work 子目录只是临时编译产物，可随时删除。";
        }

        private string DefaultStudentCode()
        {
            return
@"#include <stdio.h>
#include <stdlib.h>

char mp[20][21];
int n, score = 0, step_count = 0;
int sr[500], sc[500], len = 0;
char cur_dir = 'W';
char last_move = 'W';

int dr[4] = {-1, 0, 1, 0};
int dc[4] = {0, -1, 0, 1};
char dcname[4] = {'W', 'A', 'S', 'D'};

int opposite(char a, char b) {
    return (a == 'W' && b == 'S') || (a == 'S' && b == 'W') ||
           (a == 'A' && b == 'D') || (a == 'D' && b == 'A');
}

int abs_i(int x) { return x < 0 ? -x : x; }

int adjacent(int r1, int c1, int r2, int c2) {
    return abs_i(r1 - r2) + abs_i(c1 - c2) == 1;
}

void init_snake() {
    int used[20][20] = {0};
    int hr = -1, hc = -1;
    for (int r = 0; r < 20; r++) {
        for (int c = 0; c < 20; c++) {
            if (mp[r][c] == 'H') { hr = r; hc = c; }
        }
    }
    len = 1;
    sr[0] = hr; sc[0] = hc;
    used[hr][hc] = 1;

    while (1) {
        int found = 0;
        for (int r = 0; r < 20 && !found; r++) {
            for (int c = 0; c < 20 && !found; c++) {
                if (mp[r][c] == 'B' && !used[r][c] && adjacent(sr[len - 1], sc[len - 1], r, c)) {
                    sr[len] = r; sc[len] = c; used[r][c] = 1; len++; found = 1;
                }
            }
        }
        if (!found) break;
    }
}

void find_food(int *fr, int *fc) {
    *fr = 10; *fc = 10;
    for (int r = 0; r < 20; r++) {
        for (int c = 0; c < 20; c++) {
            if (mp[r][c] == 'F') { *fr = r; *fc = c; return; }
        }
    }
}

int safe_dir(int k) {
    int nr = sr[0] + dr[k], nc = sc[0] + dc[k];
    if (nr < 0 || nr >= 20 || nc < 0 || nc >= 20) return 0;
    if (mp[nr][nc] == '#' || mp[nr][nc] == 'O') return 0;
    int will_grow = (mp[nr][nc] == 'F') || (n > 0 && (step_count + 1) % n == 0);
    if (mp[nr][nc] == 'B') {
        if (!will_grow && nr == sr[len - 1] && nc == sc[len - 1]) return 1;
        return 0;
    }
    return 1;
}

char choose_move() {
    int fr, fc;
    find_food(&fr, &fc);
    int best = -1, best_dist = 100000;
    for (int k = 0; k < 4; k++) {
        if (opposite(cur_dir, dcname[k])) continue;
        if (!safe_dir(k)) continue;
        int nr = sr[0] + dr[k], nc = sc[0] + dc[k];
        int dist = abs_i(nr - fr) + abs_i(nc - fc);
        if (dist < best_dist) { best_dist = dist; best = k; }
    }
    if (best >= 0) return dcname[best];

    for (int k = 0; k < 4; k++) {
        if (!opposite(cur_dir, dcname[k])) return dcname[k];
    }
    return 'W';
}

void apply_move(char mv, int rr, int cc) {
    int k = 0;
    for (int i = 0; i < 4; i++) if (dcname[i] == mv) k = i;
    int nr = sr[0] + dr[k], nc = sc[0] + dc[k];
    int ate = (mp[nr][nc] == 'F');
    int grow = ate || (n > 0 && (step_count + 1) % n == 0);

    for (int i = 0; i < len; i++) mp[sr[i]][sc[i]] = '.';
    if (grow) {
        for (int i = len; i >= 1; i--) { sr[i] = sr[i - 1]; sc[i] = sc[i - 1]; }
        len++;
    } else {
        for (int i = len - 1; i >= 1; i--) { sr[i] = sr[i - 1]; sc[i] = sc[i - 1]; }
    }
    sr[0] = nr; sc[0] = nc;
    for (int i = len - 1; i >= 0; i--) mp[sr[i]][sc[i]] = (i == 0 ? 'H' : 'B');

    if (ate) {
        score += 10;
        if (rr > 0 && rr < 19 && cc > 0 && cc < 19) mp[rr][cc] = 'F';
    }
    step_count++;
    cur_dir = mv;
}

void print_final() {
    for (int r = 0; r < 20; r++) printf(""%s\n"", mp[r]);
    printf(""%d\n"", score);
    fflush(stdout);
}

int main() {
    for (int r = 0; r < 20; r++) {
        if (scanf(""%20s"", mp[r]) != 1) return 0;
    }
    if (scanf(""%d"", &n) != 1) return 0;
    init_snake();

    last_move = choose_move();
    printf(""%c\n%d\n"", last_move, score);
    fflush(stdout);

    while (1) {
        int rr, cc;
        if (scanf(""%d%d"", &rr, &cc) != 2) return 0;
        if (rr == 100 && cc == 100) {
            print_final();
            return 0;
        }
        apply_move(last_move, rr, cc);
        last_move = choose_move();
        printf(""%c\n%d\n"", last_move, score);
        fflush(stdout);
    }
}";
        }

        private sealed class RunPayload
        {
            public CompileResult Compile;
            public List<JudgeResult> Results;
            public bool ScoringRun;
            public bool UsedRandomScoringGroup;
            public string ScoringGroupLabel;
            public string CodeFingerprint;
            public DateTime RunTime;

            public RunPayload()
            {
                Compile = new CompileResult();
                Results = new List<JudgeResult>();
                ScoringRun = false;
                UsedRandomScoringGroup = false;
                ScoringGroupLabel = "";
                CodeFingerprint = "";
                RunTime = DateTime.Now;
            }
        }
    }
}
