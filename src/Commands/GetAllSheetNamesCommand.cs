using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
namespace MEP_Sheet_Manager
{
    [Transaction(TransactionMode.Manual)]
    public class GetAllSheetNamesCommand : IExternalCommand
    {
        private static SheetManagerWindow currentWindow;
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var ui = data.Application.ActiveUIDocument;
            if (ui == null || ui.Document.IsFamilyDocument) { TaskDialog.Show("Sheet Manager", "Vui lòng mở project cần xuất hoặc nhập sheet."); return Result.Cancelled; }
            try {
                if (currentWindow != null) { currentWindow.Activate(); return Result.Succeeded; }
                var window = new SheetManagerWindow(ui.Document);
                currentWindow = window;
                window.Closed += (sender, args) => currentWindow = null;
                new WindowInteropHelper(window).Owner = data.Application.MainWindowHandle;
                window.Show(); return Result.Succeeded;
            } catch (Exception ex) { message = ex.ToString(); return Result.Failed; }
        }
    }
}

