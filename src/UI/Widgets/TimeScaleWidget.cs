using HarmonyLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
#if UNHOLLOWER
using IL2CPPUtils = UnhollowerBaseLib.UnhollowerUtils;
#endif
#if INTEROP
using IL2CPPUtils = Il2CppInterop.Common.Il2CppInteropUtils;
#endif

namespace UnityExplorer.UI.Widgets
{
    internal class TimeScaleWidget
    {
        public TimeScaleWidget(GameObject parent)
        {
            Instance = this;

            this.ConstructUI(parent);

            InitPatch();
        }

        static TimeScaleWidget Instance;

        ButtonRef lockBtn;
        bool locked;
        InputFieldRef timeInput;
        float desiredTime;
        bool settingTimeScale;

        public void Update()
        {
            // Fallback in case Time.timeScale patch failed for whatever reason
            if (this.locked) this.SetTimeScale(this.desiredTime);

            if (!this.timeInput.Component.isFocused) this.timeInput.Text = Time.timeScale.ToString("F2");
        }

        void SetTimeScale(float time)
        {
            this.settingTimeScale = true;
            Time.timeScale = time;
            this.settingTimeScale = false;
        }

        // UI event listeners

        void OnTimeInputEndEdit(string val)
        {
            if (float.TryParse(val, out float f))
            {
                this.SetTimeScale(f);
                this.desiredTime = f;
            }
        }

        void OnPauseButtonClicked()
        {
            this.OnTimeInputEndEdit(this.timeInput.Text);

            this.locked = !this.locked;

            Color color = this.locked ? new Color(0.3f, 0.3f, 0.2f) : new Color(0.2f, 0.2f, 0.2f);
            RuntimeHelper.SetColorBlock(this.lockBtn.Component, color, color * 1.2f, color * 0.7f);
            this.lockBtn.ButtonText.text = this.locked ? "Unlock" : "Lock";
        }

        // UI Construction

        void ConstructUI(GameObject parent)
        {
            Text timeLabel = UIFactory.CreateLabel(parent, "TimeLabel", "Time:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(timeLabel.gameObject, minHeight: 25, minWidth: 35);

            this.timeInput = UIFactory.CreateInputField(parent, "TimeInput", "timeScale");
            UIFactory.SetLayoutElement(this.timeInput.Component.gameObject, minHeight: 25, minWidth: 40);
            this.timeInput.Component.GetOnEndEdit().AddListener(this.OnTimeInputEndEdit);

            this.timeInput.Text = string.Empty;
            this.timeInput.Text = Time.timeScale.ToString();

            this.lockBtn = UIFactory.CreateButton(parent, "PauseButton", "Lock", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(this.lockBtn.Component.gameObject, minHeight: 25, minWidth: 50);
            this.lockBtn.OnClick += this.OnPauseButtonClicked;
        }

        // Only allow Time.timeScale to be set if the user hasn't "locked" it or if we are setting the value internally.

        static void InitPatch()
        {

            try
            {
                MethodInfo target = typeof(Time).GetProperty("timeScale").GetSetMethod();
#if CPP
                if (IL2CPPUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target) == null)
                    return;
#endif
                ExplorerCore.Harmony.Patch(target,
                    prefix: new(AccessTools.Method(typeof(TimeScaleWidget), nameof(Prefix_Time_set_timeScale))));
            }
            catch { }
        }

        static bool Prefix_Time_set_timeScale()
        {
            return !Instance.locked || Instance.settingTimeScale;
        }
    }
}
