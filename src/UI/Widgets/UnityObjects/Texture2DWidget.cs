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
    public class Texture2DWidget : UnityObjectWidget
    {
        Texture2D texture;
        bool shouldDestroyTexture;

        bool textureViewerWanted;
        ButtonRef toggleButton;

        GameObject textureViewerRoot;
        InputFieldRef savePathInput;
        Image image;
        LayoutElement imageLayout;

        public override void OnBorrowed(object target, Type targetType, ReflectionInspector inspector)
        {
            base.OnBorrowed(target, targetType, inspector);

            if (target.TryCast<Cubemap>() is Cubemap cubemap)
            {
                this.texture = TextureHelper.UnwrapCubemap(cubemap);
                this.shouldDestroyTexture = true;
            }
            else if (target.TryCast<Sprite>() is Sprite sprite)
            {
                if (sprite.packingMode == SpritePackingMode.Tight)
                    this.texture = sprite.texture;
                else
                {
                    this.texture = TextureHelper.CopyTexture(sprite.texture, sprite.textureRect);
                    this.shouldDestroyTexture = true;
                }
            }
            else if (target.TryCast<Image>() is Image image)
            {
                if (image.sprite.packingMode == SpritePackingMode.Tight)
                    this.texture = image.sprite.texture;
                else
                {
                    this.texture = TextureHelper.CopyTexture(image.sprite.texture, image.sprite.textureRect);
                    this.shouldDestroyTexture = true;
                }
            }
            else
                this.texture = target.TryCast<Texture2D>();

            if (this.textureViewerRoot) this.textureViewerRoot.transform.SetParent(inspector.UIRoot.transform);

            InspectorPanel.Instance.Dragger.OnFinishResize += this.OnInspectorFinishResize;
        }

        public override void OnReturnToPool()
        {
            InspectorPanel.Instance.Dragger.OnFinishResize -= this.OnInspectorFinishResize;

            if (this.shouldDestroyTexture)
                UnityEngine.Object.Destroy(this.texture);

            this.texture = null;
            this.shouldDestroyTexture = false;

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
                this.toggleButton.ButtonText.text = "View Texture";

                this.owner.ContentRoot.SetActive(true);
            }
            else
            {
                // enable
                if (!this.image.sprite) this.SetupTextureViewer();

                this.SetImageSize();

                this.textureViewerWanted = true;
                this.textureViewerRoot.SetActive(true);
                this.toggleButton.ButtonText.text = "Hide Texture";

                this.owner.ContentRoot.gameObject.SetActive(false);
            }
        }

        void SetupTextureViewer()
        {
            if (!this.texture)
                return;

            string name = this.texture.name;
            if (string.IsNullOrEmpty(name))
                name = "untitled";
            this.savePathInput.Text = Path.Combine(ConfigManager.Default_Output_Path.Value, $"{name}.png");

            Sprite sprite = TextureHelper.CreateSprite(this.texture);
            this.image.sprite = sprite;
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
            // let unity rebuild layout etc
            yield return null;

            RectTransform imageRect = InspectorPanel.Instance.Rect;

            float rectWidth = imageRect.rect.width - 25;
            float rectHeight = imageRect.rect.height - 196;

            // If our image is smaller than the viewport, just use 100% scaling
            if (this.texture.width < rectWidth && this.texture.height < rectHeight)
            {
                this.imageLayout.minWidth = this.texture.width;
                this.imageLayout.minHeight = this.texture.height;
            }
            else // we will need to scale down the image to fit
            {
                // get the ratio of our viewport dimensions to width and height
                float viewWidthRatio = (float)((decimal)rectWidth / (decimal)this.texture.width);
                float viewHeightRatio = (float)((decimal)rectHeight / (decimal)this.texture.height);

                // if width needs to be scaled more than height
                if (viewWidthRatio < viewHeightRatio)
                {
                    this.imageLayout.minWidth = this.texture.width * viewWidthRatio;
                    this.imageLayout.minHeight = this.texture.height * viewWidthRatio;
                }
                else // if height needs to be scaled more than width
                {
                    this.imageLayout.minWidth = this.texture.width * viewHeightRatio;
                    this.imageLayout.minHeight = this.texture.height * viewHeightRatio;
                }
            }
        }

        void OnSaveTextureClicked()
        {
            if (!this.texture)
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

            TextureHelper.SaveTextureAsPNG(this.texture, path);
        }

        public override GameObject CreateContent(GameObject uiRoot)
        {
            GameObject ret = base.CreateContent(uiRoot);

            // Button

            this.toggleButton = UIFactory.CreateButton(this.UIRoot, "TextureButton", "View Texture", new Color(0.2f, 0.3f, 0.2f));
            this.toggleButton.Transform.SetSiblingIndex(0);
            UIFactory.SetLayoutElement(this.toggleButton.Component.gameObject, minHeight: 25, minWidth: 150);
            this.toggleButton.OnClick += this.ToggleTextureViewer;

            // Texture viewer

            this.textureViewerRoot = UIFactory.CreateVerticalGroup(uiRoot, "TextureViewer", false, false, true, true, 2, new Vector4(5, 5, 5, 5),
                new Color(0.1f, 0.1f, 0.1f), childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.textureViewerRoot, flexibleWidth: 9999, flexibleHeight: 9999);

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
