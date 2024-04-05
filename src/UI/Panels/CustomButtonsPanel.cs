using UnityExplorer.CSConsole;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Panels;

public class CustomButtonsPanel : UEPanel
{
    public override string Name => "CustomButtons";
    public override UIManager.Panels PanelType => UIManager.Panels.Custom;
    
    public InputFieldRef Input;

    public override int MinWidth => 750;
    public override int MinHeight => 300;
    public override Vector2 DefaultAnchorMin => new(0.4f, 0.175f);
    public override Vector2 DefaultAnchorMax => new(0.85f, 0.925f);
    public Text InputText { get; private set; }
    
    public Action OnGetBeschleuniger;
    public Action OnGetUser;
    public Action OnGetResources;
    public Action<string> OnInputChanged;
    public Action OnSendScrolls;
    public Action OnSendMarch;
    public Action OnSendEther;
    public Action OnPanelResized;

    public CustomButtonsPanel(UIBase owner) : base(owner)
    {
    }

    // UI Construction

    public override void OnFinishResize()
    {
        this.OnPanelResized?.Invoke();
    }

    protected override void ConstructPanelContent()
    {
        GameObject inputArea = UIFactory.CreateUIObject("CustomInputGroup", this.ContentRoot);
        UIFactory.SetLayoutElement(inputArea, flexibleWidth: 9999, flexibleHeight: 0, minHeight:20);
        UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(inputArea, false, true, true, true);
        
        // sendMailRow

        GameObject sendMailRow = UIFactory.CreateVerticalGroup(this.ContentRoot, "SendMailRow", false, false, true, true,
            5, new Vector4(8, 8, 10, 5),
            default, TextAnchor.MiddleLeft);
        UIFactory.SetLayoutElement(sendMailRow, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

        
        // input field

        int fontSize = 16;

        this.Input = UIFactory.CreateInputField(inputArea, "UserIdInput", "120370304");
        UIFactory.SetLayoutElement(this.Input.UIRoot, minHeight: 25, flexibleWidth: 9999);
        this.Input.OnValueChanged += this.InvokeOnValueChanged;

        this.InputText = this.Input.Component.textComponent;
        this.InputText.supportRichText = false;
        this.InputText.font = UniversalUI.ConsoleFont;
        this.InputText.color = Color.white;
        this.Input.Component.customCaretColor = true;
        this.Input.Component.caretColor = Color.white;
        this.Input.PlaceholderText.fontSize = fontSize;
        this.InputText.font = UniversalUI.ConsoleFont;
        this.Input.PlaceholderText.font = UniversalUI.ConsoleFont;

        // Buttons
        
        ButtonRef sendEtherButton =
            UIFactory.CreateButton(sendMailRow, "SendEther", "Sende Äther", new Color(0.33f, 0.5f, 0.33f));
        UIFactory.SetLayoutElement(sendEtherButton.Component.gameObject, minHeight: 28, minWidth: 130, flexibleHeight: 0);
        sendEtherButton.ButtonText.fontSize = 15;
        sendEtherButton.OnClick += () => { this.OnSendEther?.Invoke(); };

        ButtonRef sendScrollsButton =
            UIFactory.CreateButton(sendMailRow, "SendScrolls", "Sende Schriftrollen", new Color(0.33f, 0.33f, 0.33f));
        UIFactory.SetLayoutElement(sendScrollsButton.Component.gameObject, minHeight: 28, minWidth: 130, flexibleHeight: 0);
        sendScrollsButton.ButtonText.fontSize = 15;
        sendScrollsButton.OnClick += () => { this.OnSendScrolls?.Invoke(); };
        
        ButtonRef sendMarchButton =
            UIFactory.CreateButton(sendMailRow, "SendMarch", "Sende Marsch", new Color(0.33f, 0.33f, 0.33f));
        UIFactory.SetLayoutElement(sendMarchButton.Component.gameObject, minHeight: 28, minWidth: 130, flexibleHeight: 0);
        sendMarchButton.ButtonText.fontSize = 15;
        sendMarchButton.OnClick += () => { this.OnSendMarch?.Invoke(); };
        
        ButtonRef countResources =
            UIFactory.CreateButton(sendMailRow, "Count Resources", "Resourcen", new Color(0.33f, 0.33f, 0.33f));
        UIFactory.SetLayoutElement(countResources.Component.gameObject, minHeight: 28, minWidth: 130, flexibleHeight: 0);
        countResources.ButtonText.fontSize = 15;
        countResources.OnClick += () => { this.OnGetResources?.Invoke(); };
        
        ButtonRef countBeschleuniger =
            UIFactory.CreateButton(sendMailRow, "Count Beschleuniger", "Beschleuniger", new Color(0.33f, 0.33f, 0.33f));
        UIFactory.SetLayoutElement(countBeschleuniger.Component.gameObject, minHeight: 28, minWidth: 130, flexibleHeight: 0);
        countBeschleuniger.ButtonText.fontSize = 15;
        countBeschleuniger.OnClick += () => { this.OnGetBeschleuniger?.Invoke(); };
        
        ButtonRef getUser =
            UIFactory.CreateButton(sendMailRow, "Get User", "Get User", new Color(0.33f, 0.33f, 0.33f));
        UIFactory.SetLayoutElement(getUser.Component.gameObject, minHeight: 28, minWidth: 130, flexibleHeight: 0);
        getUser.ButtonText.fontSize = 15;
        getUser.OnClick += () => { this.OnGetUser?.Invoke(); };
        
    }

    private void InvokeOnValueChanged(string value)
    {
        if (value.Length == UniversalUI.MAX_INPUTFIELD_CHARS)
            ExplorerCore.LogWarning($"Reached maximum InputField character length! ({UniversalUI.MAX_INPUTFIELD_CHARS})");

        this.OnInputChanged?.Invoke(value);
    }
}