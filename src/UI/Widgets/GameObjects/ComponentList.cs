﻿using UnityExplorer.Inspectors;
using UniverseLib.UI.Widgets.ButtonList;
using UniverseLib.UI.Widgets.ScrollView;
using Object = UnityEngine.Object;

namespace UnityExplorer.UI.Widgets
{
    public class ComponentList : ButtonListHandler<Component, ComponentCell>
    {
        public GameObjectInspector Parent;

        public ComponentList(ScrollPool<ComponentCell> scrollPool, Func<List<Component>> getEntriesMethod)
            : base(scrollPool, getEntriesMethod, null, null, null)
        {
            this.SetICell = this.SetComponentCell;
            this.ShouldDisplay = this.CheckShouldDisplay;
            this.OnCellClicked = this.OnComponentClicked;
        }

        public void Clear()
        {
            this.RefreshData();
            this.ScrollPool.Refresh(true, true);
        }

        private bool CheckShouldDisplay(Component _, string __) => true;

        public override void OnCellBorrowed(ComponentCell cell)
        {
            base.OnCellBorrowed(cell);

            cell.OnBehaviourToggled += this.OnBehaviourToggled;
            cell.OnDestroyClicked += this.OnDestroyClicked;
        }

        public override void SetCell(ComponentCell cell, int index)
        {
            base.SetCell(cell, index);
        }

        private void OnComponentClicked(int index)
        {
            List<Component> entries = this.GetEntries();

            if (index < 0 || index >= entries.Count)
                return;

            ExplorerCore.LogWarning("Clicked");
            Component comp = entries[index];
            if (comp)
            {
                ExplorerCore.LogWarning(comp);
                InspectorManager.Inspect(comp);
            }
        }

        private void OnBehaviourToggled(bool value, int index)
        {
            try
            {
                List<Component> entries = this.GetEntries();
                Component comp = entries[index];

                if (comp.TryCast<Behaviour>() is Behaviour behaviour)
                    behaviour.enabled = value;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception toggling Behaviour.enabled: {ex.ReflectionExToString()}");
            }
        }

        private void OnDestroyClicked(int index)
        {
            try
            {
                List<Component> entries = this.GetEntries();
                Component comp = entries[index];

                Object.DestroyImmediate(comp);

                this.Parent.UpdateComponents();
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception destroying Component: {ex.ReflectionExToString()}");
            }
        }

        private static readonly Dictionary<string, string> compToStringCache = new();

        // Called from ButtonListHandler.SetCell, will be valid
        private void SetComponentCell(ComponentCell cell, int index)
        {
            List<Component> entries = this.GetEntries();
            cell.Enable();

            Component comp = entries[index];
            Type type = comp.GetActualType();

            if (!compToStringCache.ContainsKey(type.AssemblyQualifiedName))
                compToStringCache.Add(type.AssemblyQualifiedName, SignatureHighlighter.Parse(type, true));

            cell.Button.ButtonText.text = compToStringCache[type.AssemblyQualifiedName];

            if (typeof(Behaviour).IsAssignableFrom(type))
            {
                cell.BehaviourToggle.interactable = true;
                cell.BehaviourToggle.Set(comp.TryCast<Behaviour>().enabled, false);
                cell.BehaviourToggle.graphic.color = new Color(0.8f, 1, 0.8f, 0.3f);
            }
            else
            {
                cell.BehaviourToggle.interactable = false;
                cell.BehaviourToggle.Set(true, false);
                //RuntimeHelper.SetColorBlock(cell.BehaviourToggle,)
                cell.BehaviourToggle.graphic.color = new Color(0.2f, 0.2f, 0.2f);
            }

            // if component is the first index it must be the transform, dont show Destroy button for it.
            if (index == 0 && cell.DestroyButton.Component.gameObject.activeSelf)
                cell.DestroyButton.Component.gameObject.SetActive(false);
            else if (index > 0 && !cell.DestroyButton.Component.gameObject.activeSelf)
                cell.DestroyButton.Component.gameObject.SetActive(true);
        }
    }
}
