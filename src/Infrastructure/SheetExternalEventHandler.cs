using System;
using Autodesk.Revit.UI;

namespace MEP_Sheet_Manager
{
    internal sealed class SheetExternalEventHandler : IExternalEventHandler
    {
        public Action<UIApplication> Request { get; set; }
        public void Execute(UIApplication application)
        {
            var action = Request;
            Request = null;
            if (action != null) action(application);
        }
        public string GetName() { return "MEP Sheet Studio requests"; }
    }
}
