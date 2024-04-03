using HarmonyLib;
using System.Collections.Specialized;
using UnityExplorer.UI.Panels;
using UniverseLib.UI;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.Hooks
{
    public class HookList : ICellPoolDataSource<HookCell>
    {
        public int ItemCount => currentHooks.Count;
        
        internal static readonly HashSet<string> hookedSignatures = new();
        internal static readonly OrderedDictionary currentHooks = new();

        internal static GameObject UIRoot;
        internal static ScrollPool<HookCell> HooksScrollPool;

        public static void EnableOrDisableHookClicked(int index)
        {
            HookInstance hook = (HookInstance)currentHooks[index];
            hook.TogglePatch();

            HooksScrollPool.Refresh(true, false);
        }
        
        public static void EnableOrDisableOnStartupHookClicked(int index)
        {
            HookInstance hook = (HookInstance)currentHooks[index];
            hook.Startup();

            HooksScrollPool.Refresh(true, false);
        }

        public static void DeleteHookClicked(int index, bool deleteFile)
        {
            HookInstance hook = (HookInstance)currentHooks[index];

            if (HookCreator.CurrentEditedHook == hook)
                HookCreator.EditorInputCancel();

            if (deleteFile)
            {
                RemoveSavedHook(hook);
            }
            hook.Unpatch();
            currentHooks.RemoveAt(index);
            hookedSignatures.Remove(hook.TargetMethod.FullDescription());

            HooksScrollPool.Refresh(true, false);
        }
        
        private static void RemoveSavedHook(HookInstance hook)
        {
            HookCreator.HookData data = new HookCreator.HookData();
            data.Description = hook.TargetMethod.FullDescription();
            data.ReflectedType = hook.TargetMethod.ReflectedType.ToString();
            data.SourceCode = hook.PatchSourceCode;
            string filename = data.Description.GetHashCode().ToString("X8") + ".txt";
            string folderpath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UnityExplorerHarmonyHooks");
            Directory.CreateDirectory(folderpath);
            string fpath = Path.Combine(folderpath, filename);
            if (File.Exists(fpath))
            {
                File.Delete(fpath);
                ExplorerCore.Log($"File {filename} deleted successfully.");
            }
            else
            {
                ExplorerCore.Log($"file: {filename} not found to delete");
            }
            
        }

        public static void EditPatchClicked(int index)
        {
            if (HookCreator.PendingGeneric)
                HookManagerPanel.genericArgsHandler.Cancel();

            HookManagerPanel.Instance.SetPage(HookManagerPanel.Pages.HookSourceEditor);
            HookInstance hook = (HookInstance)currentHooks[index];
            HookCreator.SetEditedHook(hook);
        }

        // Set current hook cell

        public void OnCellBorrowed(HookCell cell) { }

        public void SetCell(HookCell cell, int index)
        {
            if (index >= currentHooks.Count)
            {
                cell.Disable();
                return;
            }

            cell.CurrentDisplayedIndex = index;
            HookInstance hook = (HookInstance)currentHooks[index];

            cell.MethodNameLabel.text = SignatureHighlighter.ParseMethod(hook.TargetMethod);

            cell.ToggleActiveButton.ButtonText.text = hook.Enabled ? "On" : "Off";
            cell.ToogleActiveOnStartupButton.ButtonText.text = hook.StartUp ? "Y" : "N";
            RuntimeHelper.SetColorBlockAuto(cell.ToggleActiveButton.Component,
                hook.Enabled ? new Color(0.15f, 0.2f, 0.15f) : new Color(0.2f, 0.2f, 0.15f));
            RuntimeHelper.SetColorBlockAuto(cell.ToogleActiveOnStartupButton.Component,
                hook.StartUp ? new Color(0.15f, 0.2f, 0.15f) : new Color(0.2f, 0.2f, 0.15f));
        }

        // UI

        internal void ConstructUI(GameObject leftGroup)
        {
            UIRoot = UIFactory.CreateUIObject("CurrentHooksPanel", leftGroup);
            UIFactory.SetLayoutElement(UIRoot, preferredHeight: 150, flexibleHeight: 0, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(UIRoot, true, true, true, true);

            Text hooksLabel = UIFactory.CreateLabel(UIRoot, "HooksLabel", "Current Hooks", TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(hooksLabel.gameObject, minHeight: 30, flexibleWidth: 9999);

            HooksScrollPool = UIFactory.CreateScrollPool<HookCell>(UIRoot, "HooksScrollPool",
                out GameObject hooksScroll, out GameObject hooksContent);
            UIFactory.SetLayoutElement(hooksScroll, flexibleHeight: 9999);
            HooksScrollPool.Initialize(this);
        }
    }
}
