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
        Panel.OnGetShareRewards += GetShareRewards;
        Panel.OnGetUser += GetUser;
        Panel.OnGetUserByName += GetUserByName;
        Panel.ActivateScan += ActivateScan;
        Panel.ActivateKdG += ActivateKdG;
        Panel.ActivateShowdown += ActivateShowdown;
        Panel.HuntDwarfs += HuntDwarfs;
        Panel.KickFromRallys += KickFromRallys;
        Panel.GetUserBalloons += GetUserBalloons;
        Panel.GetAllianceBalloons += GetAllianceBalloons;
        Panel.SearchKriecher += SearchKriecher;
        Panel.LogAll += LogAll;
    }



    private static void OnInputChanged(string value)
    {
        // ExplorerCore.Log($"new input: {value}");
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


    List<DB.ConsumableItemData> resource = ItemBag.Instance.GetItemsByCategory(""accelerate"", t);
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
    
    public static void GetUser()
    {
        var text = @"Hashtable argsTable = new Hashtable
{
    {""target_uid"", " + GetText() + @"}
};
var hubPort = new HubPort(""Player:loadUserBasicInfo"");
hubPort.SendRequest(argsTable, null, false);
";
        Evaluate(text);
    }
    
    private static void ActivateShowdown()
    {
        var text = @"
if(CustomHelper.Features.Feature.Contains(""AM:accept""))
{
    CustomHelper.Features.Feature.Remove(""AM:accept""); 
    CustomHelper.Features.Feature.Remove(""Legend2:templeSummon"");
    Log(""Showdown deactivated"");
}
else
{
    CustomHelper.Features.Feature.Add(""AM:accept""); 
    CustomHelper.Features.Feature.Add(""Legend2:templeSummon"");
    Log(""Showdown activated"");
}
";
        Evaluate(text);
    }

    private static void ActivateKdG()
    {
        var text = @"
if(CustomHelper.Features.Feature.Contains(""StAvaWater:getActivityInfo""))
{
    CustomHelper.Features.Feature.Remove(""StAvaWater:getActivityInfo"");
    CustomHelper.Features.Feature.Remove(""Alliance:getAllianceDetailInfo"");
    CustomHelper.Features.Feature.Remove(""Player:loadUserBasicInfo"");
    Log(""KdG deactivated"");
}
else
{
    CustomHelper.Features.Feature.Add(""StAvaWater:getActivityInfo"");
    CustomHelper.Features.Feature.Add(""Alliance:getAllianceDetailInfo"");
    CustomHelper.Features.Feature.Add(""Player:loadUserBasicInfo"");
    Log(""KdG activated"");
}";
        Evaluate(text);
    }

    private static void ActivateScan()
    {
        var inputText = GetText();
        var kingdom = inputText.Length != 5 ? 10090 : int.Parse(inputText);
        
        var textCoro = @"public class MyCoro
{
    public static IEnumerator Main()
    {
        PVPMap map = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(PVPMap))[0] as PVPMap;
        CustomHelper.Requested.Uids.Clear();
        map.GotoLocation(new Coordinate("+kingdom+@",10,10), false);

        int currentX = 10;
        int currentY = 0;
        int minX = 10;
        int minY = 10;
        int steps = 50;
        int maxX = 1270;
        int maxY = 2555;

        while(currentY + steps < maxY)
        {
            while(currentX + steps < maxX)
            {
                currentX += steps;
                map.GotoLocation(new Coordinate("+kingdom+@",currentX,currentY), false);
                yield return new WaitForSeconds(0.01f);
            }
            currentX = minX;
            currentY += steps;
            map.GotoLocation(new Coordinate("+kingdom+@",currentX,currentY), false);
            yield return new WaitForSeconds(0.01f);
        }
    }
}";
        
        Evaluate(textCoro);
        
        var text = @"
CustomHelper.Features.Feature.Add(""Map:enterKingdomBlock"");
CustomHelper.Features.Feature.Add(""Player:loadUserBasicInfo"");
Start(MyCoro.Main());";
        Evaluate(text);
    }

    public static void GetUserByName()
    {
        var parts = GetText().Split('|');
        var kingdom = parts[1];
        var user = parts[0];
        int maxLevel = 100;
        if (parts.Length > 2)
        {
            maxLevel = int.Parse(parts[2]);
        }

        var text = @"var name = """+user+@""";
var kingdom = """+kingdom+@""";
var maxLevel = "+maxLevel+@";

Hashtable argsTable = new Hashtable();
argsTable.Add(""name"", name); // Example of successfully retrieved UIDs
var hubPort = new HubPort(""player:searchUser"");
var users = new List<string>();
var infoMapping = new Queue<LoaderInfo>();
hubPort.SendRequest(argsTable, (success, response) =>
        {
            if (success)
            {
                System.Collections.ArrayList al = response as System.Collections.ArrayList;
			foreach(var id in al)
                {
                    Hashtable t = id as Hashtable;
                     string tName = t[""name""] as string; 
                    bool isMatch = tName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 &&(string.IsNullOrEmpty(kingdom) || t[""world_id""].Equals(kingdom));

                    if (isMatch)
                    {
                              
                        var uid = t[""uid""] as string;
                        argsTable = new Hashtable
                        {
                            {""custom"", ""248fd869-fe82-49cb-aef3-2a4cd78b76f4""},
                            {""target_uid"", uid}
                        };

                        var rm = RequestManager.inst;
                        var info = rm.SendRequest(""Player:loadUserBasicInfo"", argsTable, null, false);
                        infoMapping.Enqueue(info);

                        info.OnResult += (action) => {

                            var res = CustomHelper.RequestResult.GetAndRemove(infoMapping.Dequeue());
                            string jsonResponse = res;

                            var data = Newtonsoft.Json.Linq.JObject.Parse(jsonResponse);

                            // Extract the values
                            var userCity = data[""data""][""set""][""user_city""][0];
                            var userInfo = data[""data""][""set""][""user_info""][0];
                            string pre = string.Empty;
                            if (data != null && 
                                data[""data""] != null && 
                                data[""data""][""set""] != null && 
                                data[""data""][""set""][""alliance""] != null)
                            {
                                var alliance = data[""data""][""set""][""alliance""][0];
                                string allianceAb = alliance != null ? (string)alliance[""acronym""] : string.Empty;
                                pre = !string.IsNullOrEmpty(allianceAb) ? $""({allianceAb})"" : string.Empty;
                            }
                            
                            string userName = (string)userInfo[""name""];
                            int mapX = (int)userCity[""map_x""];
                            int mapY = (int)userCity[""map_y""];
                            int level = (int)userCity[""level""];
                            int worldId = (int)userCity[""world_id""];
                            if(maxLevel == -1 || level > maxLevel)
                            {
                                return;
                            }

                            Log($""{pre}{userName}, SH {level}, {mapX}:{mapY}, world_id: {worldId}"");
                        };
                    }
                }
                
            }
            else
                Log(""Lambda request failed."");
        }, false);
";
        Evaluate(text);
    }
    
    public static void GetShareRewards()
    {
        var text = @"Hashtable argsTable = new Hashtable();
argsTable.Add(""config_id"", 215000006);
var hubPort = new HubPort(""PostCard:postCardReward"");
hubPort.SendRequest(argsTable, null, false);
";
        Evaluate(text);
    }
    
    public static void LogAll()
    {
        var text = @"if(CustomHelper.Features.Feature.Contains(""LogAll""))
{
    foreach(var feature in CustomHelper.Features.Feature)
    {
        Log($""Removing feature {feature}"");
    }
    CustomHelper.Features.Feature.Clear();
    Log(""All logs deactivated"");
}
else
{
    CustomHelper.Features.Feature.Add(""LogAll"");
    Log(""All logs activated"");
}
";
        Evaluate(text);
    }
    
    public static void HuntDwarfs()
    {
        var inputText = GetText();
        if (!int.TryParse(inputText, out var amount))
        {
            amount = 5;
        }
        
        var text = @"int count = 5;
for(int i = 0; i < count; i++)
{
    Hashtable argsTable = new Hashtable();
    argsTable.Add(""act_id"", 234300001);
    argsTable.Add(""custom"", ""248fd869-fe82-49cb-aef3-2a4cd78b76f4"");

    var rm = RequestManager.inst;
    var info = rm.SendRequest(""TreasureActivity2:genNpc"", argsTable, null, false);

    info.OnResult += (action) => 
    {
        try 
        {
            var res = CustomHelper.RequestResult.GetAndRemove(info);
            string jsonResponse = res;

            var data = Newtonsoft.Json.Linq.JObject.Parse(jsonResponse);
        
            // Extract the values
            var userCity = data[""data""][""set""][""user_kingdom_map""][0];
            int mapX = (int)userCity[""map_x""];
            int mapY = (int)userCity[""map_y""];
            int slotId = (int)userCity[""slot_id""];

            argsTable = new Hashtable();
            argsTable.Add(""x"", mapX);
            argsTable.Add(""slot_id"", 2);
            argsTable.Add(""act_id"", 234300001);
            argsTable.Add(""y"", mapY);
            var hubPort = new HubPort(""TreasureActivity2:startVisitNpc"");
            hubPort.SendRequest(argsTable, null, false);
        }
        catch(Exception e)
        {
            Log(e.Message);
        }
    };
}
";
        Evaluate(text);
    }
    
    public static void KickFromRallys()
    {
        var inputText = GetText();
        
        var text = @"Hashtable argsTable = new Hashtable();
argsTable.Add(""m"", 0);
argsTable.Add(""k"", 0);
argsTable.Add(""r"", 4);
 argsTable.Add(""custom"", ""248fd869-fe82-49cb-aef3-2a4cd78b76f4"");

var rm = RequestManager.inst;
var info = rm.SendRequest(""Alliance:loadWarList"", argsTable, null, false);

info.OnResult += (action) => {

Log(""Test"");
var kickuid = DB.DBManager.inst.DB_UserProfileDB._profiles[""uid""];
	Log(kickuid);
var res = CustomHelper.RequestResult.GetAndRemove(info);
var data = Newtonsoft.Json.Linq.JObject.Parse(res);
        
            // Extract the values
foreach(var rally in data[""data""][""set""][""rally""])
{
		var slotsInfo = rally[""slots_info""];
		foreach (var key in slotsInfo)
            {
				if(key.Path.ToString().Contains(kickuid.ToString()))
                {
                    var rallyId = rally[""rally_id""].ToString();
				var leader = rally[""uid""].ToString();
                            var kickArgs = new Hashtable
                            {
                                {""cancel_uid"", kickuid},
                                {""uid"", leader},
                                {""rally_id"", rallyId}
                            };
                
                            var acceptHub = new HubPort(""rally:repatriate"");
                            acceptHub.SendRequest(kickArgs, null, false);
                            Log($""kicked: {kickuid} from rally {rallyId} by {leader}"");
                }

            }
}

};
";
        Evaluate(text);
    }
    
    public static void GetUserBalloons()
    {
        var inputText = GetText();
        
        var text = @"for(var ballonNumber = 1; ballonNumber <= 4; ballonNumber++)
            {
                Hashtable userArgsTable = new Hashtable
                {
                    {""cell_id"", ballonNumber},
                    {""owner_uid"", "+inputText+@"}
                };

                var hubPort = new HubPort(""Freight:getFreightPos"");
                //Log(Newtonsoft.Json.JsonConvert.SerializeObject(userArgsTable));

                // Assign the delegate to handle the OnResult
                Action<bool, object> handleUserBalloonInfo = (successInner, response) =>
                {
                    if(successInner)
                    {
                        Hashtable al = response as Hashtable;
                        //Log(Newtonsoft.Json.JsonConvert.SerializeObject(response));

                        if (al == null) return;

                        var name = "+inputText+@";
                        var mapX = al[""x""];
                        var mapY = al[""y""];
                        var worldId = al[""world_id""];

                        Log($""{name} - {mapX}:{mapY}:{worldId}"");
                    }
                    
                };

                // Assign the OnResult event to the explicit delegate
                hubPort.SendRequest(userArgsTable, handleUserBalloonInfo, false);
            }
";
        Evaluate(text);
    }
    
    public static void GetAllianceBalloons()
    {
        var inputText = GetText();
        
        var text = @"
var rm = RequestManager.inst;
Hashtable argsTable = new Hashtable();
argsTable.Add(""alliance_id"", "+inputText+@");
argsTable.Add(""custom"", ""248fd869-fe82-49cb-aef3-2a4cd78b76f4"");

// Define explicit delegate for OnResult handler
Action<bool, object> handleUserBalloonInfo = null; 

// Send request and assign the main handler
//hubPort.SendRequest(argsTable, handleGetAllianceUsers, false);
var loadAllianceMemberInfo = rm.SendRequest(""Alliance:getAllianceMemberInfo"", argsTable, null, false);
var capturedLoadAllianceMemberInfo = loadAllianceMemberInfo; // Capture info to avoid hoisting issues

// Define the main handler to process the response from player:getonlineusers
Action<object> handleGetAllianceUsers = (actionOuter) =>
{

        var loadUserBasicInfoRes = CustomHelper.RequestResult.GetAndRemove(capturedLoadAllianceMemberInfo);
        var capturedLoadAllianceMemberInfoRes = Newtonsoft.Json.Linq.JObject.Parse(loadUserBasicInfoRes);
        //Log(loadUserBasicInfoRes);
        if (capturedLoadAllianceMemberInfoRes == null) return;

        //Log(""Something returned"");

        var memberList = capturedLoadAllianceMemberInfoRes[""data""][""set""][""user_info""];
        Log($""Alliance users: {memberList.Count()}"");

        foreach (var member in memberList)
        {
            var capturedMember = member; // Capture ID to avoid hoisting issues
            /*if(capturedMember[""uid""].ToString() != ""120370304"")
            {
               continue;
            }

            Log(""Found user Dolgsthrasir"");*/

            for(var ballonNumber = 1; ballonNumber <= 4; ballonNumber++)
            {
                Hashtable userArgsTable = new Hashtable
                {
                    {""cell_id"", ballonNumber},
                    {""owner_uid"", capturedMember[""uid""].ToString()}
                };

                var hubPort = new HubPort(""Freight:getFreightPos"");
                //Log(Newtonsoft.Json.JsonConvert.SerializeObject(userArgsTable));

                // Assign the delegate to handle the OnResult
                handleUserBalloonInfo = (successInner, response) =>
                {
                    if(successInner)
                    {
                        Hashtable al = response as Hashtable;
                        //Log(Newtonsoft.Json.JsonConvert.SerializeObject(response));

                        if (al == null) return;

                        var name = capturedMember[""name""];
                        var mapX = al[""x""];
                        var mapY = al[""y""];
                        var worldId = al[""world_id""];

                        Log($""{name} - {mapX}:{mapY}:{worldId}"");
                    }
                    
                };

                // Assign the OnResult event to the explicit delegate
                hubPort.SendRequest(userArgsTable, handleUserBalloonInfo, false);
            }
            
        }
        
};


loadAllianceMemberInfo.OnResult += (action) => handleGetAllianceUsers(action);
";
        Evaluate(text);
    }
    
    public static void SearchKriecher()
    {
        var inputText = GetText();
        var distance = string.IsNullOrEmpty(inputText) ? 100 : int.Parse(inputText);
        var text = @"public class KriecherCoro
{
    public static IEnumerator Main()
    {
        int distance = int.Parse("""+inputText+@""");
        if(distance <= 0)
        {
            distance = 100;
        }
        CustomHelper.Features.Feature.Add(""Map:enterKingdomBlock"");
        CustomHelper.Features.Feature.Add(""Kriecher"");
        PVPMap map = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(PVPMap))[0] as PVPMap;
        CustomHelper.Features.Feature.RemoveWhere(entry => entry.StartsWith(""5er Kriecher""));
        CustomHelper.Features.Feature.RemoveWhere(entry => entry.StartsWith(""6er Kriecher""));
        CustomHelper.Features.Feature.RemoveWhere(entry => entry.StartsWith(""7er Kriecher""));

        int world_id = int.Parse(map.CurrentKXY.K.ToString());
        int currentX = int.Parse(map.CurrentKXY.X.ToString());
        int currentY = int.Parse(map.CurrentKXY.Y.ToString());
        int originX = currentX;
        int originY = currentY;
        int minX = currentX - 100;
        int minY = currentY - 100;
        int steps = 50;
        int maxX = currentX + 100;
        int maxY = currentY + distance > 1250 ? 1250 : currentY + distance;

        currentX = minX;
        currentY = minY;
        while(currentY + steps < maxY)
        {
            while(currentX + steps < maxX)
            {
                currentX += steps;
                // map.Goto(new Vector3(steps,0,0), new Vector3(0,0,0));
                map.GotoLocation(new Coordinate(world_id,currentX,currentY), false);
                yield return new WaitForSeconds(0.01f);
            }
            currentX = minX;
            currentY += steps;
            // map.Goto(new Vector3(-2000,0,-steps), new Vector3(0,0,0));
            map.GotoLocation(new Coordinate(world_id,currentX,currentY), false);
            yield return new WaitForSeconds(0.01f);
            //cam.SetCameraPositionByDir(new Vector3(currentX,z,currentY),false);
        }

        map.GotoLocation(new Coordinate(world_id,originX,originY), false);

    }
}
";
        Evaluate(text);
        
        Evaluate(@"Start(KriecherCoro.Main());");
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