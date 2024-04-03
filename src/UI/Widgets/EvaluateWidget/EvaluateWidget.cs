using UnityExplorer.CacheObject;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.UI.Widgets
{
    public class EvaluateWidget : IPooledObject
    {
        public CacheMember Owner { get; set; }

        public GameObject UIRoot { get; set; }
        public float DefaultHeight => -1f;

        private ParameterInfo[] parameters;
        internal GameObject parametersHolder;
        private ParameterHandler[] paramHandlers;

        private Type[] genericArguments;
        internal GameObject genericArgumentsHolder;
        private GenericArgumentHandler[] genericHandlers;

        public void OnBorrowedFromPool(CacheMember owner)
        {
            this.Owner = owner;

            this.parameters = owner.Arguments;
            this.paramHandlers = new ParameterHandler[this.parameters.Length];

            this.genericArguments = owner.GenericArguments;
            this.genericHandlers = new GenericArgumentHandler[this.genericArguments.Length];

            this.SetArgRows();

            this.UIRoot.SetActive(true);
        }

        public void OnReturnToPool()
        {
            foreach (ParameterHandler widget in this.paramHandlers)
            {
                widget.OnReturned();
                Pool<ParameterHandler>.Return(widget);
            }
            this.paramHandlers = null;

            foreach (GenericArgumentHandler widget in this.genericHandlers)
            {
                widget.OnReturned();
                Pool<GenericArgumentHandler>.Return(widget);
            }
            this.genericHandlers = null;

            this.Owner = null;
        }

        public Type[] TryParseGenericArguments()
        {
            Type[] outArgs = new Type[this.genericArguments.Length];

            for (int i = 0; i < this.genericArguments.Length; i++)
                outArgs[i] = this.genericHandlers[i].Evaluate();

            return outArgs;
        }

        public object[] TryParseArguments()
        {
            if (!this.parameters.Any())
                return ArgumentUtility.EmptyArgs;

            object[] outArgs = new object[this.parameters.Length];

            for (int i = 0; i < this.parameters.Length; i++)
                outArgs[i] = this.paramHandlers[i].Evaluate();

            return outArgs;
        }

        private void SetArgRows()
        {
            if (this.genericArguments.Any())
            {
                this.genericArgumentsHolder.SetActive(true);
                this.SetGenericRows();
            }
            else
                this.genericArgumentsHolder.SetActive(false);

            if (this.parameters.Any())
            {
                this.parametersHolder.SetActive(true);
                this.SetNormalArgRows();
            }
            else
                this.parametersHolder.SetActive(false);
        }

        private void SetGenericRows()
        {
            for (int i = 0; i < this.genericArguments.Length; i++)
            {
                Type type = this.genericArguments[i];

                GenericArgumentHandler holder = this.genericHandlers[i] = Pool<GenericArgumentHandler>.Borrow();
                holder.UIRoot.transform.SetParent(this.genericArgumentsHolder.transform, false);
                holder.OnBorrowed(type);
            }
        }

        private void SetNormalArgRows()
        {
            for (int i = 0; i < this.parameters.Length; i++)
            {
                ParameterInfo param = this.parameters[i];

                ParameterHandler holder = this.paramHandlers[i] = Pool<ParameterHandler>.Borrow();
                holder.UIRoot.transform.SetParent(this.parametersHolder.transform, false);
                holder.OnBorrowed(param);
            }
        }


        public GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "EvaluateWidget", false, false, true, true, 3, new Vector4(2, 2, 2, 2),
                new Color(0.15f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(this.UIRoot, minWidth: 50, flexibleWidth: 9999, minHeight: 50, flexibleHeight: 800);
            //UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // generic args
            this.genericArgumentsHolder = UIFactory.CreateUIObject("GenericHolder", this.UIRoot);
            UIFactory.SetLayoutElement(this.genericArgumentsHolder, flexibleWidth: 1000);
            Text genericsTitle = UIFactory.CreateLabel(this.genericArgumentsHolder, "GenericsTitle", "Generic Arguments", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(genericsTitle.gameObject, minHeight: 25, flexibleWidth: 1000);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.genericArgumentsHolder, false, false, true, true, 3);
            UIFactory.SetLayoutElement(this.genericArgumentsHolder, minHeight: 25, flexibleHeight: 750, minWidth: 50, flexibleWidth: 9999);
            //genericArgHolder.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // args
            this.parametersHolder = UIFactory.CreateUIObject("ArgHolder", this.UIRoot);
            UIFactory.SetLayoutElement(this.parametersHolder, flexibleWidth: 1000);
            Text argsTitle = UIFactory.CreateLabel(this.parametersHolder, "ArgsTitle", "Arguments", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(argsTitle.gameObject, minHeight: 25, flexibleWidth: 1000);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.parametersHolder, false, false, true, true, 3);
            UIFactory.SetLayoutElement(this.parametersHolder, minHeight: 25, flexibleHeight: 750, minWidth: 50, flexibleWidth: 9999);
            //argHolder.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // evaluate button
            ButtonRef evalButton = UIFactory.CreateButton(this.UIRoot, "EvaluateButton", "Evaluate", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(evalButton.Component.gameObject, minHeight: 25, minWidth: 150, flexibleWidth: 0);
            evalButton.OnClick += () =>
            {
                this.Owner.EvaluateAndSetCell();
            };

            return this.UIRoot;
        }
    }
}
