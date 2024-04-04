using System.Text;
using Mono.CSharp;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;
using UniverseLib.Input;
using UniverseLib.UI.Models;

namespace UnityExplorer.CSConsole;

public static class CustomButtonController
{
    public static ScriptEvaluator Evaluator { get; private set; }
    public static CustomButtonsPanel Panel => UIManager.GetPanel<CustomButtonsPanel>(UIManager.Panels.Custom);
    public static InputFieldRef Input => Panel.Input;

    static HashSet<string> usingDirectives;
    static StringBuilder evaluatorOutput;
    static StringWriter evaluatorStringWriter;
    static float timeOfLastCtrlR;

    static bool settingCaretCoroutine;
    static string previousInput;
    static int previousContentLength = 0;

    static readonly string[] DefaultUsing = new string[]
    {
        "System", "System.Linq", "System.Text", "System.Collections", "System.Collections.Generic", "System.Reflection",
        "UnityEngine", "UniverseLib",
#if CPP
            "UnhollowerBaseLib",
            "UnhollowerRuntimeLib",
#endif
    };

    public static void Init()
    {
        try
        {
            ResetConsole(false);
            // ensure the compiler is supported (if this fails then SRE is probably stripped)
            Evaluator.Compile("0 == 0");
        }
        catch (Exception ex)
        {
            return;
        }

        Panel.OnInputChanged += OnInputChanged;
        Panel.OnSendEther += SendEther;
        Panel.OnSendMarch += SendMarch;
        Panel.OnSendScrolls += SendScrolls;
        Panel.OnGetResources += GetResources;
        Panel.OnGetBeschleuniger += GetBeschleuniger;
    }

    private static void OnInputChanged(string value)
    {
        ExplorerCore.Log($"new input: {value}");
        // prevent escape wiping input
        if (InputManager.GetKeyDown(KeyCode.Escape))
        {
            Input.Text = previousInput;

            return;
        }

        previousInput = value;
    }


    #region Evaluating

    static void GenerateTextWriter()
    {
        evaluatorOutput = new StringBuilder();
        evaluatorStringWriter = new StringWriter(evaluatorOutput);
    }

    public static void ResetConsole(bool logSuccess = true)
    {
        if (Evaluator != null)
            Evaluator.Dispose();

        GenerateTextWriter();
        Evaluator = new ScriptEvaluator(evaluatorStringWriter)
        {
            InteractiveBaseClass = typeof(ScriptInteraction)
        };

        usingDirectives = new HashSet<string>();
        foreach (string use in DefaultUsing)
            AddUsing(use);

    }

    public static void AddUsing(string assemblyName)
    {
        if (!usingDirectives.Contains(assemblyName))
        {
            Evaluate($"using {assemblyName};", true);
            usingDirectives.Add(assemblyName);
        }
    }

    public static string GetText()
    {
        return string.IsNullOrEmpty(Input.Text) ? Input.PlaceholderText.text : Input.Text;
    }

    public static void SendMarch()
    {
        ExplorerCore.Log($"current Input: {Input.Text}");
        ExplorerCore.Log($"current Input2: {GetText()}");

        var text = @"Hashtable argsTable = new Hashtable();
argsTable.Add(""text"", ""Take this!"");
argsTable.Add(""goods_id"", 6000041);
argsTable.Add(""to_uids"", new string[] { """ + GetText() + @""" });
argsTable.Add(""amount"", 2);
var hubPort = new HubPort(""Mail:sendMail"");
hubPort.SendRequest(argsTable, null, false);";
        Evaluate(text);
    }
    
    public static void SendScrolls()
    {
        var text = @"Hashtable argsTable = new Hashtable();
argsTable.Add(""text"", ""Take this!"");
argsTable.Add(""goods_id"", 6008759);
argsTable.Add(""to_uids"", new string[] { """ + GetText() + @""" });
argsTable.Add(""amount"", 500);
var hubPort = new HubPort(""Mail:sendMail"");
hubPort.SendRequest(argsTable, null, false);";
        Evaluate(text);
    }
    
    public static void SendEther()
    {
        var text = @"Hashtable argsTable = new Hashtable();
argsTable.Add(""text"", ""Take this!"");
argsTable.Add(""goods_id"", 6007178);
argsTable.Add(""to_uids"", new string[] { """ + GetText() + @""" });
argsTable.Add(""amount"", 2);
var hubPort = new HubPort(""Mail:sendMail"");
hubPort.SendRequest(argsTable, null, false);";
        Evaluate(text);
    }
    
    public static void GetResources()
    {
        var text = @"var types = new ItemBag.ItemType[] {ItemBag.ItemType.gold, ItemBag.ItemType.food, ItemBag.ItemType.silver, ItemBag.ItemType.ore, ItemBag.ItemType.wood};
foreach(var t in types)
{
    string name = """";
    switch (t)
    {
        case ItemBag.ItemType.ore:
            name = ""Stone"";
            break;
        case ItemBag.ItemType.wood:
            name = ""Obsidian"";
            break;
        default:
            name = t.ToString();
            break;
    }


    List<DB.ConsumableItemData> resource = ItemBag.Instance.GetItemsByCategory(""resources"", t);
    long amount = 0;
    foreach(var f in resource)
    {
        var quantity = f.quantity;
        var info = f.itemInfo;
        var val = (long)info.Value;
        amount += (quantity * val);
        
    }
	Log($""{name}: {amount.ToString(""N0"")}"");
}
";
        Evaluate(text);
    }
    
    public static void GetBeschleuniger()
    {
        var text = @"var types = new ItemBag.ItemType[] {ItemBag.ItemType.healing_speedup, ItemBag.ItemType.speedup, ItemBag.ItemType.training_speedup, ItemBag.ItemType.building_speedup, ItemBag.ItemType.research_speedup};
foreach(var t in types)
{
    string name = """";
    switch (t)
    {
        case ItemBag.ItemType.speedup:
            name = ""Allgemein"";
            break;
        case ItemBag.ItemType.training_speedup:
            name = ""Training"";
            break;
        case ItemBag.ItemType.building_speedup:
            name = ""Ausbau"";
            break;
        case ItemBag.ItemType.research_speedup:
            name = ""Forschung"";
            break;
        case ItemBag.ItemType.healing_speedup:
            name = ""Heilung"";
            break;
        default:
            name = t.ToString();
            break;
    }


    List<DB.ConsumableItemData> resource = ItemBag.Instance.GetItemsByCategory(""resources"", t);
    long seconds = 0;
    foreach(var f in resource)
    {
        var quantity = f.quantity;
        var info = f.itemInfo;
        var val = (long)info.Value;
        seconds += (quantity * val);
        
    }
	Log($""{name}: minutes {seconds / 60} - hours {seconds / 60.0d / 60.0d} - days {seconds / 60.0d / 60.0d / 24.0d}"");
}
";
        Evaluate(text);
    }

    public static void Evaluate(string input, bool supressLog = false)
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
                    string result = ret?.ToString();
                    if (!string.IsNullOrEmpty(result))
                        ExplorerCore.Log($"Invoked REPL, result: {ret}");
                    else
                        ExplorerCore.Log($"Invoked REPL (no return value)");
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

    #endregion
}