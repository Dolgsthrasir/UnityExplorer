using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Mono.CSharp;
using UnityExplorer.Config;
using UnityExplorer.CSConsole;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.UI.Panels
{
    public class LogPanel : UEPanel, ICellPoolDataSource<ConsoleLogCell>
    {
        public struct LogInfo
        {
            public string message;
            public LogType type;

            public LogInfo(string message, LogType type) { this.message = message; this.type = type; }
        }

        private static readonly List<LogInfo> Logs = new();
        private static string CurrentStreamPath;

        public override string Name => "Log";
        public override UIManager.Panels PanelType => UIManager.Panels.ConsoleLog;

        public override int MinWidth => 350;
        public override int MinHeight => 75;
        public override Vector2 DefaultAnchorMin => new(0.5f, 0.03f);
        public override Vector2 DefaultAnchorMax => new(0.9f, 0.2f);

        public override bool ShouldSaveActiveState => true;
        public override bool ShowByDefault => true;

        public int ItemCount => Logs.Count;

        private static ScrollPool<ConsoleLogCell> logScrollPool;

        public LogPanel(UIBase owner) : base(owner)
        {
            this.SetupIO();
            GenerateTextWriter();
            Evaluator = new ScriptEvaluator(evaluatorStringWriter)
            {
                InteractiveBaseClass = typeof(ScriptInteraction)
            };
        }
        
        private static void GenerateTextWriter()
        {
            evaluatorOutput = new StringBuilder();
            evaluatorStringWriter = new StringWriter(evaluatorOutput);
        }

        public static ScriptEvaluator Evaluator { get; set; }

        private static bool DoneScrollPoolInit;

        public override void SetActive(bool active)
        {
            base.SetActive(active);

            if (active && !DoneScrollPoolInit)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(this.Rect);
                logScrollPool.Initialize(this);
                DoneScrollPoolInit = true;
            }

            logScrollPool.Refresh(true, false);
        }

        private void SetupIO()
        {
            var fileName = $"UnityExplorer {DateTime.Now:u}.txt";
            fileName = IOUtility.EnsureValidFilename(fileName);
            var path = Path.Combine(ExplorerCore.ExplorerFolder, "Logs");
            CurrentStreamPath = IOUtility.EnsureValidFilePath(Path.Combine(path, fileName));

            // clean old log(s)
            var files = Directory.GetFiles(path);
            if (files.Length >= 10)
            {
                var sorted = files.ToList();
                // sort by 'datetime.ToString("u")' will put the oldest ones first
                sorted.Sort();
                for (var i = 0; i < files.Length - 9; i++)
                    File.Delete(files[i]);
            }

            File.WriteAllLines(CurrentStreamPath, Logs.Select(it => it.message).ToArray());
        }

        // Logging

        public static void Log(string message, LogType type)
        {
            Logs.Add(new LogInfo(message, type));

            if (CurrentStreamPath != null)
                File.AppendAllText(CurrentStreamPath, '\n' + message);

            if (logScrollPool != null)
                logScrollPool.Refresh(true, false);
        }

        private static void ClearLogs()
        {
            Logs.Clear();
            logScrollPool.Refresh(true, true);
        }

        private static void OpenLogFile()
        {
            if (File.Exists(CurrentStreamPath))
                Process.Start(CurrentStreamPath);
        }

        // Cell pool

        private static readonly Dictionary<LogType, Color> logColors = new()
        {
            { LogType.Log,       Color.white },
            { LogType.Warning,   Color.yellow },
            { LogType.Assert,    Color.yellow },
            { LogType.Error,     Color.red },
            { LogType.Exception, Color.red },
        };

        private readonly Color logEvenColor = new(0.34f, 0.34f, 0.34f);
        private readonly Color logOddColor = new(0.28f, 0.28f, 0.28f);
        private static StringBuilder evaluatorOutput;
        private static TextWriter evaluatorStringWriter;

        public void OnCellBorrowed(ConsoleLogCell cell) { }

        public void SetCell(ConsoleLogCell cell, int index)
        {
            if (index >= Logs.Count)
            {
                cell.Disable();
                return;
            }

            // Logs are displayed in reverse order (newest at top)
            index = Logs.Count - index - 1;

            var log = Logs[index];
            cell.IndexLabel.text = $"{index}:";
            cell.Input.Text = log.message;
            cell.Input.Component.textComponent.color = logColors[log.type];
            cell.GotoButton.Enabled = cell.ButtonEnabled;
            if (cell.GotoButton.Enabled)
            {
                cell.GotoButton.GameObject.SetActive(true);
                cell.ButtonAction = Evaluate;
            }

            var color = index % 2 == 0 ? this.logEvenColor : this.logOddColor;
            RuntimeHelper.SetColorBlock(cell.Input.Component, color);
        }

        private static void Evaluate(string input, bool supressLog = false)
        {
            if (evaluatorStringWriter == null || evaluatorOutput == null)
            {
                GenerateTextWriter();
                Evaluator._textWriter = evaluatorStringWriter;
            }

            try
            {
                // Compile the code. If it returned a CompiledMethod, it is REPL.
                CompiledMethod repl = Evaluator.Compile(input);

                if (repl != null)
                {
                    // Valid REPL, we have a delegate to the evaluation.
                    try
                    {
                        object ret = null;
                        repl.Invoke(ref ret);
                    }
                    catch (Exception ex)
                    {
                        ExplorerCore.LogWarning($"Exception invoking REPL: {ex}");
                    }
                }
                else
                {
                    // The compiled code was not REPL, so it was a using directive or it defined classes.

                    string output = Evaluator._textWriter.ToString();
                    string[] outputSplit = output.Split('\n');
                    if (outputSplit.Length >= 2)
                        output = outputSplit[outputSplit.Length - 2];
                    evaluatorOutput.Clear();

                    if (ScriptEvaluator._reportPrinter.ErrorsCount > 0)
                        throw new FormatException($"Unable to compile the code. Evaluator's last output was:\r\n{output}");
                    else if (!supressLog)
                        ExplorerCore.Log($"Code compiled without errors.");
                }
            }
            catch (FormatException fex)
            {
                if (!supressLog)
                    ExplorerCore.LogWarning(fex.Message);
            }
            catch (Exception ex)
            {
                if (!supressLog)
                    ExplorerCore.LogWarning(ex);
            }
        }

        // UI Construction

        protected override void ConstructPanelContent()
        {
            // Log scroll pool

            logScrollPool = UIFactory.CreateScrollPool<ConsoleLogCell>(this.ContentRoot, "Logs", out var scrollObj,
                out var scrollContent, new Color(0.03f, 0.03f, 0.03f));
            UIFactory.SetLayoutElement(scrollObj, flexibleWidth: 9999, flexibleHeight: 9999);

            // Buttons and toggles

            var optionsRow = UIFactory.CreateUIObject("OptionsRow", this.ContentRoot);
            UIFactory.SetLayoutElement(optionsRow, minHeight: 25, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(optionsRow, false, false, true, true, 5, 2, 2, 2, 2);

            var clearButton = UIFactory.CreateButton(optionsRow, "ClearButton", "Clear", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(clearButton.Component.gameObject, minHeight: 23, flexibleHeight: 0, minWidth: 60);
            clearButton.OnClick += ClearLogs;
            clearButton.Component.transform.SetSiblingIndex(1);

            var fileButton = UIFactory.CreateButton(optionsRow, "FileButton", "Open Log File", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(fileButton.Component.gameObject, minHeight: 23, flexibleHeight: 0, minWidth: 100);
            fileButton.OnClick += OpenLogFile;
            fileButton.Component.transform.SetSiblingIndex(2);

            var unityToggle = UIFactory.CreateToggle(optionsRow, "UnityLogToggle", out var toggle, out var toggleText);
            UIFactory.SetLayoutElement(unityToggle, minHeight: 25, minWidth: 150);
            toggleText.text = "Log Unity Debug?";
            toggle.isOn = ConfigManager.Log_Unity_Debug.Value;
            ConfigManager.Log_Unity_Debug.OnValueChanged += (bool val) => toggle.isOn = val;
            toggle.onValueChanged.AddListener((bool val) => ConfigManager.Log_Unity_Debug.Value = val);
        }
    }

    #region Log Cell View

    public class ConsoleLogCell : ICell
    {
        public Text IndexLabel;
        public InputFieldRef Input;
        public ButtonRef GotoButton;
        public RectTransform Rect { get; set; }
        public GameObject UIRoot { get; set; }

        public float DefaultHeight => 25;

        public bool Enabled => this.UIRoot.activeInHierarchy;
        public void Enable() => this.UIRoot.SetActive(true);
        public void Disable() => this.UIRoot.SetActive(false);
        
        public bool ButtonEnabled
        {
            get { 
                var input = this.Input.Text;

                // Define the regex pattern to capture x, y, and world
                var pattern = @"(\d+):(\d+):(\d+)";
                var regex = new Regex(pattern);

                // Match the pattern in the input string
                var match = regex.Match(input);

                return match.Success;
             }
        }

        public Action<string, bool> ButtonAction { get; set; }

        public GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateUIObject("LogCell", parent, new Vector2(25, 25));
            this.Rect = this.UIRoot.GetComponent<RectTransform>();
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(this.UIRoot, false, false, true, true, 3);
            UIFactory.SetLayoutElement(this.UIRoot, minHeight: 25, minWidth: 50, flexibleWidth: 9999);

            this.IndexLabel = UIFactory.CreateLabel(this.UIRoot, "IndexLabel", "i:", TextAnchor.MiddleCenter, Color.grey, false, 12);
            UIFactory.SetLayoutElement(this.IndexLabel.gameObject, minHeight: 25, minWidth: 30, flexibleWidth: 40);

            this.Input = UIFactory.CreateInputField(this.UIRoot, "Input", "");
            //Input.Component.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIFactory.SetLayoutElement(this.Input.UIRoot, minHeight: 25, flexibleWidth: 9999);
            RuntimeHelper.SetColorBlock(this.Input.Component, new Color(0.1f, 0.1f, 0.1f), new Color(0.13f, 0.13f, 0.13f),
                new Color(0.07f, 0.07f, 0.07f));
            this.Input.Component.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

            this.GotoButton = UIFactory.CreateButton(this.UIRoot, "GoToCoords", "GoTo", new Color(0.2f, 0.27f, 0.2f));
            UIFactory.SetLayoutElement(this.GotoButton.Component.gameObject, minHeight: 25, minWidth: 20);
            RuntimeHelper.SetColorBlock(this.GotoButton.Component, new Color(0.1f, 0.1f, 0.1f), new Color(0.13f, 0.13f, 0.13f),
                new Color(0.07f, 0.07f, 0.07f));
            this.GotoButton.Component.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            this.GotoButton.OnClick += this.GotoClicked;
            this.GotoButton.Enabled = false;
            this.GotoButton.GameObject.SetActive(false);

            this.Input.Component.readOnly = true;
            this.Input.Component.textComponent.supportRichText = true;
            this.Input.Component.lineType = InputField.LineType.MultiLineNewline;
            this.Input.Component.textComponent.font = UniversalUI.ConsoleFont;
            this.Input.PlaceholderText.font = UniversalUI.ConsoleFont;

            return this.UIRoot;
        }

        private void GotoClicked()
        {
            var input = this.Input.Text;

            // Define the regex pattern to capture x, y, and world
            var pattern = @"(\d+):(\d+):(\d+)";
            var regex = new Regex(pattern);

            // Match the pattern in the input string
            var match = regex.Match(input);

            if (match.Success)
            {
                // Extract the values
                var x = match.Groups[1].Value;
                var y = match.Groups[2].Value;
                var world = match.Groups[3].Value;
                
                this.ButtonAction.Invoke($@"
PVPMap map = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(PVPMap))[0] as PVPMap;
map.GotoLocation(new Coordinate({world},{x},{y}), false);", false);
            }
            else
            {
                Console.WriteLine("No coordinates found.");
            }
        }
    }

    #endregion
}
