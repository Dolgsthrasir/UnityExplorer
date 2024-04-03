using System.Collections;
using UnityExplorer.Config;
using UnityExplorer.Inspectors;
using UnityExplorer.UI.Panels;
using UniverseLib.Runtime;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.UI.Widgets
{
    public class MaterialWidget : UnityObjectWidget
    {
        static MaterialWidget()
        {
            mi_GetTexturePropertyNames = typeof(Material).GetMethod("GetTexturePropertyNames", ArgumentUtility.EmptyTypes);
            MaterialWidgetSupported = mi_GetTexturePropertyNames != null;
        }

        internal static bool MaterialWidgetSupported { get; }
        static readonly MethodInfo mi_GetTexturePropertyNames;

        Material material;
        Texture2D activeTexture;
        readonly Dictionary<string, Texture> textures = new();
        readonly HashSet<Texture2D> texturesToDestroy = new();

        bool textureViewerWanted;
        ButtonRef toggleButton;

        GameObject textureViewerRoot;
        Dropdown textureDropdown;
        InputFieldRef savePathInput;
        Image image;
        LayoutElement imageLayout;

        public override void OnBorrowed(object target, Type targetType, ReflectionInspector inspector)
        {
            base.OnBorrowed(target, targetType, inspector);

            this.material = target.TryCast<Material>();

            if (this.material.mainTexture) this.SetActiveTexture(this.material.mainTexture);

            if (mi_GetTexturePropertyNames.Invoke(this.material, ArgumentUtility.EmptyArgs) is IEnumerable<string> propNames)
            {
                foreach (string property in propNames)
                {
                    if (this.material.GetTexture(property) is Texture texture)
                    {
                        if (texture.TryCast<Texture2D>() is null && texture.TryCast<Cubemap>() is null)
                            continue;

                        this.textures.Add(property, texture);

                        if (!this.activeTexture) this.SetActiveTexture(texture);
                    }
                }
            }

            if (this.textureViewerRoot)
            {
                this.textureViewerRoot.transform.SetParent(inspector.UIRoot.transform);
                this.RefreshTextureDropdown();
            }

            InspectorPanel.Instance.Dragger.OnFinishResize += this.OnInspectorFinishResize;
        }

        void SetActiveTexture(Texture texture)
        {
            if (texture.TryCast<Texture2D>() is Texture2D tex2D)
                this.activeTexture = tex2D;
            else if (texture.TryCast<Cubemap>() is Cubemap cubemap)
            {
                this.activeTexture = TextureHelper.UnwrapCubemap(cubemap);
                this.texturesToDestroy.Add(this.activeTexture);
            }
        }

        public override void OnReturnToPool()
        {
            InspectorPanel.Instance.Dragger.OnFinishResize -= this.OnInspectorFinishResize;

            if (this.texturesToDestroy.Any())
            {
                foreach (Texture2D tex in this.texturesToDestroy)
                    UnityEngine.Object.Destroy(tex);
                this.texturesToDestroy.Clear();
            }

            this.material = null;
            this.activeTexture = null;
            this.textures.Clear();

            if (this.image.sprite)
                UnityEngine.Object.Destroy(this.image.sprite);

            if (this.textureViewerWanted) this.ToggleTextureViewer();

            if (this.textureViewerRoot) this.textureViewerRoot.transform.SetParent(Pool<Texture2DWidget>.Instance.InactiveHolder.transform);

            base.OnReturnToPool();
        }

        void ToggleTextureViewer()
        {
            if (this.textureViewerWanted)
            {
                // disable

                this.textureViewerWanted = false;
                this.textureViewerRoot.SetActive(false);
                this.toggleButton.ButtonText.text = "View Material";

                this.owner.ContentRoot.SetActive(true);
            }
            else
            {
                // enable

                if (!this.image.sprite)
                {
                    this.RefreshTextureViewer();
                    this.RefreshTextureDropdown();
                }

                this.SetImageSize();

                this.textureViewerWanted = true;
                this.textureViewerRoot.SetActive(true);
                this.toggleButton.ButtonText.text = "Hide Material";

                this.owner.ContentRoot.gameObject.SetActive(false);
            }
        }

        void RefreshTextureViewer()
        {
            if (!this.activeTexture)
            {
                ExplorerCore.LogWarning($"Material has no active textures!");
                this.savePathInput.Text = string.Empty;
                return;
            }

            if (this.image.sprite)
                UnityEngine.Object.Destroy(this.image.sprite);

            string name = this.activeTexture.name;
            if (string.IsNullOrEmpty(name))
                name = "untitled";
            this.savePathInput.Text = Path.Combine(ConfigManager.Default_Output_Path.Value, $"{name}.png");

            Sprite sprite = TextureHelper.CreateSprite(this.activeTexture);
            this.image.sprite = sprite;
        }

        void RefreshTextureDropdown()
        {
            if (!this.textureDropdown)
                return;

            this.textureDropdown.options.Clear();

            foreach (string key in this.textures.Keys) this.textureDropdown.options.Add(new(key));

            int i = 0;
            foreach (Texture value in this.textures.Values)
            {
                if (this.activeTexture.ReferenceEqual(value))
                {
                    this.textureDropdown.value = i;
                    break;
                }
                i++;
            }

            this.textureDropdown.RefreshShownValue();
        }

        void OnTextureDropdownChanged(int value)
        {
            Texture tex = this.textures.ElementAt(value).Value;
            if (this.activeTexture.ReferenceEqual(tex))
                return;
            this.SetActiveTexture(tex);
            this.RefreshTextureViewer();
        }

        void OnInspectorFinishResize()
        {
            this.SetImageSize();
        }

        void SetImageSize()
        {
            if (!this.imageLayout)
                return;

            RuntimeHelper.StartCoroutine(this.SetImageSizeCoro());
        }

        IEnumerator SetImageSizeCoro()
        {
            if (!this.activeTexture)
                yield break;

            // let unity rebuild layout etc
            yield return null;

            RectTransform imageRect = InspectorPanel.Instance.Rect;

            float rectWidth = imageRect.rect.width - 25;
            float rectHeight = imageRect.rect.height - 196;

            // If our image is smaller than the viewport, just use 100% scaling
            if (this.activeTexture.width < rectWidth && this.activeTexture.height < rectHeight)
            {
                this.imageLayout.minWidth = this.activeTexture.width;
                this.imageLayout.minHeight = this.activeTexture.height;
            }
            else // we will need to scale down the image to fit
            {
                // get the ratio of our viewport dimensions to width and height
                float viewWidthRatio = (float)((decimal)rectWidth / (decimal)this.activeTexture.width);
                float viewHeightRatio = (float)((decimal)rectHeight / (decimal)this.activeTexture.height);

                // if width needs to be scaled more than height
                if (viewWidthRatio < viewHeightRatio)
                {
                    this.imageLayout.minWidth = this.activeTexture.width * viewWidthRatio;
                    this.imageLayout.minHeight = this.activeTexture.height * viewWidthRatio;
                }
                else // if height needs to be scaled more than width
                {
                    this.imageLayout.minWidth = this.activeTexture.width * viewHeightRatio;
                    this.imageLayout.minHeight = this.activeTexture.height * viewHeightRatio;
                }
            }
        }

        void OnSaveTextureClicked()
        {
            if (!this.activeTexture)
            {
                ExplorerCore.LogWarning("Texture is null, maybe it was destroyed?");
                return;
            }

            if (string.IsNullOrEmpty(this.savePathInput.Text))
            {
                ExplorerCore.LogWarning("Save path cannot be empty!");
                return;
            }

            string path = this.savePathInput.Text;
            if (!path.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
                path += ".png";

            path = IOUtility.EnsureValidFilePath(path);

            if (File.Exists(path))
                File.Delete(path);

            TextureHelper.SaveTextureAsPNG(this.activeTexture, path);
        }

        public override GameObject CreateContent(GameObject uiRoot)
        {
            GameObject ret = base.CreateContent(uiRoot);

            // Button

            this.toggleButton = UIFactory.CreateButton(this.UIRoot, "MaterialButton", "View Material", new Color(0.2f, 0.3f, 0.2f));
            this.toggleButton.Transform.SetSiblingIndex(0);
            UIFactory.SetLayoutElement(this.toggleButton.Component.gameObject, minHeight: 25, minWidth: 150);
            this.toggleButton.OnClick += this.ToggleTextureViewer;

            // Texture viewer

            this.textureViewerRoot = UIFactory.CreateVerticalGroup(uiRoot, "MaterialViewer", false, false, true, true, 2, new Vector4(5, 5, 5, 5),
                new Color(0.1f, 0.1f, 0.1f), childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.textureViewerRoot, flexibleWidth: 9999, flexibleHeight: 9999);

            // Buttons holder

            GameObject dropdownRow = UIFactory.CreateHorizontalGroup(this.textureViewerRoot, "DropdownRow", false, true, true, true, 5, new(3, 3, 3, 3));
            UIFactory.SetLayoutElement(dropdownRow, minHeight: 30, flexibleWidth: 9999);

            Text dropdownLabel = UIFactory.CreateLabel(dropdownRow, "DropdownLabel", "Texture:");
            UIFactory.SetLayoutElement(dropdownLabel.gameObject, minWidth: 75, minHeight: 25);

            GameObject dropdownObj = UIFactory.CreateDropdown(dropdownRow, "TextureDropdown", out this.textureDropdown, "NOT SET", 13, this.OnTextureDropdownChanged);
            UIFactory.SetLayoutElement(dropdownObj, minWidth: 350, minHeight: 25);

            // Save helper

            GameObject saveRowObj = UIFactory.CreateHorizontalGroup(this.textureViewerRoot, "SaveRow", false, false, true, true, 2, new Vector4(2, 2, 2, 2),
                new Color(0.1f, 0.1f, 0.1f));

            ButtonRef saveBtn = UIFactory.CreateButton(saveRowObj, "SaveButton", "Save .PNG", new Color(0.2f, 0.25f, 0.2f));
            UIFactory.SetLayoutElement(saveBtn.Component.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 0);
            saveBtn.OnClick += this.OnSaveTextureClicked;

            this.savePathInput = UIFactory.CreateInputField(saveRowObj, "SaveInput", "...");
            UIFactory.SetLayoutElement(this.savePathInput.UIRoot, minHeight: 25, minWidth: 100, flexibleWidth: 9999);

            // Actual texture viewer

            GameObject imageViewport = UIFactory.CreateVerticalGroup(this.textureViewerRoot, "ImageViewport", false, false, true, true,
                bgColor: new(1, 1, 1, 0), childAlignment: TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(imageViewport, flexibleWidth: 9999, flexibleHeight: 9999);

            GameObject imageHolder = UIFactory.CreateUIObject("ImageHolder", imageViewport);
            this.imageLayout = UIFactory.SetLayoutElement(imageHolder, 1, 1, 0, 0);

            GameObject actualImageObj = UIFactory.CreateUIObject("ActualImage", imageHolder);
            RectTransform actualRect = actualImageObj.GetComponent<RectTransform>();
            actualRect.anchorMin = new(0, 0);
            actualRect.anchorMax = new(1, 1);
            this.image = actualImageObj.AddComponent<Image>();

            this.textureViewerRoot.SetActive(false);

            return ret;
        }
    }
}
