using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using MEP_Sheet_Manager;
[Transaction(TransactionMode.Manual)]
public class RevitSheetTests : IExternalCommand {
 public Result Execute(ExternalCommandData data,ref string message,ElementSet elements) {
  Document doc=null;var lines=new List<string>();
  string folder=@"D:\2_Revit\6_Working\00. Revit_API\Project\MEP_Sheet_Manager_Ver1\tests\artifacts";
  Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);lines.Add("PASS: "+label);};
  var svc=typeof(SheetInfo).Assembly.GetType("MEP_Sheet_Manager.SheetService");
  Func<string,object[],object> call=(name,args)=>svc.GetMethod(name,BindingFlags.Static|BindingFlags.Public).Invoke(null,args);
  try {
   Directory.CreateDirectory(folder);
   doc=data.Application.Application.NewProjectDocument(UnitSystem.Metric);
   var source=new List<SheetInfo>{new SheetInfo("001.01","Điện tầng 1",false),new SheetInfo("A-002","Điện tầng 1",false),new SheetInfo("P-003","Sheet dự kiến",true)};
   string path=Path.Combine(folder,"RevitImport.xlsx"); SheetWorkbook.Write(path,source);
   var rows=SheetWorkbook.Read(path);
   check((int)call("Validate",new object[]{doc,rows})==0,"valid import preview in new project");
   check((int)call("Create",new object[]{doc,rows,ElementId.InvalidElementId})==3,"create 3 sheets from Excel in a new project");
   var actual=(List<SheetInfo>)call("Read",new object[]{doc});
   check(actual.Count==3 && actual.Any(s=>s.Number=="001.01" && s.Name=="Điện tầng 1"),"exact names, numbers and leading zeros");
   check(actual.Single(s=>s.Number=="P-003").IsPlaceholder,"placeholder creation");
   check((int)call("Create",new object[]{doc,rows,ElementId.InvalidElementId})==0,"repeat import skips existing numbers");
   var duplicate=new List<SheetInfo>{new SheetInfo("DUP","One",false),new SheetInfo("DUP","Two",false)};
   check((int)call("Validate",new object[]{doc,duplicate})==2,"reject duplicate numbers in Excel");
   var missing=new List<SheetInfo>{new SheetInfo("","One",false),new SheetInfo("N-1","",false)};
   check((int)call("Validate",new object[]{doc,missing})==2,"reject missing names and numbers");
   var rollback=new List<SheetInfo>{new SheetInfo("RB-1","Temporary placeholder",true),new SheetInfo("RB-2","Invalid title block",false)};
   bool failed=false;try{call("Create",new object[]{doc,rollback,new ElementId(123456789)});}catch(TargetInvocationException){failed=true;}
   var after=(List<SheetInfo>)call("Read",new object[]{doc});
   check(failed && after.Count==3 && !after.Any(s=>s.Number=="RB-1"),"transaction rolls back all sheets after creation failure");
   var placementService=typeof(SheetInfo).Assembly.GetType("MEP_Sheet_Manager.FloorPlanPlacementService");
   Func<string,object[],object> placement=(name,args)=>placementService.GetMethod(name,BindingFlags.Static|BindingFlags.Public).Invoke(null,args);
   ViewPlan plan;
   using(var tx=new Transaction(doc,"Prepare floor plan test")) {
    tx.Start();
    var level=Level.Create(doc,0);
    var type=new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t=>t.ViewFamily==ViewFamily.FloorPlan);
    plan=ViewPlan.Create(doc,type.Id,level.Id);plan.Name="Floor plan placement test";
    tx.Commit();
   }
   var assignments=(List<SheetViewAssignment>)placement("Read",new object[]{doc});
   check(assignments.Count==2 && assignments.All(r=>r.FloorPlans.Any(p=>p.Id==plan.Id.IntegerValue)),"floor plan choices per real sheet, excludes placeholders");
   var choice=assignments[0].FloorPlans.Single(p=>p.Id==plan.Id.IntegerValue);
   assignments[0].SelectedFloorPlan=choice;assignments[1].SelectedFloorPlan=choice;
   bool duplicatePlan=false;try{placement("Place",new object[]{doc,assignments});}catch(TargetInvocationException){duplicatePlan=true;}
   check(duplicatePlan && new FilteredElementCollector(doc).OfClass(typeof(Viewport)).GetElementCount()==0,"same floor plan on two sheets rejected before creating viewports");
   assignments[1].SelectedFloorPlan=assignments[1].FloorPlans[0];
   bool missingScope=false;try{placement("Place",new object[]{doc,assignments});}catch(TargetInvocationException){missingScope=true;}
   check(missingScope && new FilteredElementCollector(doc).OfClass(typeof(Viewport)).GetElementCount()==0,"automatic placement requires valid paper bounds before mutation on a sheet without title block");
   assignments[0].SetPosition(321.25,198.75);
   check((int)placement("Place",new object[]{doc,assignments})==1,"place selected floor plan on selected sheet");
   var viewport=new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Single();
   check(viewport.ViewId==plan.Id && viewport.SheetId.IntegerValue==assignments[0].SheetId,"viewport belongs to exact chosen sheet and plan");
   check((int)placement("Place",new object[]{doc,assignments})==1 && new FilteredElementCollector(doc).OfClass(typeof(Viewport)).GetElementCount()==1,"repeat placement repositions existing viewport without duplicating it");
   var refreshed=(List<SheetViewAssignment>)placement("Read",new object[]{doc});
   check(refreshed.Where(r=>r.SheetId!=assignments[0].SheetId).All(r=>!r.FloorPlans.Any(p=>p.Id==plan.Id.IntegerValue)) && refreshed.Single(r=>r.SheetId==assignments[0].SheetId).CurrentFloorPlans.Contains(plan.Name),"placed plan is available only on its own sheet for layout editing");
   var edit=refreshed.Single(r=>r.SheetId==assignments[0].SheetId);
   edit.SelectedFloorPlan=edit.FloorPlans.Single(p=>p.Id==plan.Id.IntegerValue);edit.SetPosition(321.25,198.75);
   check((int)placement("Place",new object[]{doc,new List<SheetViewAssignment>{edit}})==1,"saved layout moves existing viewport without duplicating it");
   var moved=new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Single().GetBoxCenter();
   check(Math.Abs(UnitUtils.ConvertFromInternalUnits(moved.X,UnitTypeId.Millimeters)-321.25)<0.01 && Math.Abs(UnitUtils.ConvertFromInternalUnits(moved.Y,UnitTypeId.Millimeters)-198.75)<0.01,"saved millimeter coordinates produce exact viewport box center");
   var manualRows=new List<SheetInfo>{new SheetInfo("MAN-001","Sheet thủ công",false),new SheetInfo("MAN-002","Placeholder thủ công",true)};
   string jsonPath=Path.Combine(folder,"RevitManualSheets.json");SheetJsonFile.Write(jsonPath,manualRows);
   check((int)call("Create",new object[]{doc,SheetJsonFile.Read(jsonPath),ElementId.InvalidElementId})==2,"create manually entered sheet and placeholder from exported JSON");
   var jsonActual=(List<SheetInfo>)call("Read",new object[]{doc});
   check(jsonActual.Any(s=>s.Number=="MAN-001" && s.Name=="Sheet thủ công" && !s.IsPlaceholder) && jsonActual.Any(s=>s.Number=="MAN-002" && s.IsPlaceholder),"manual/JSON import preserves names, numbers and placeholder states in Revit");
   var deletion=jsonActual.Where(s=>s.Number.StartsWith("MAN-")).ToList();
   check((int)call("Delete",new object[]{doc,deletion})==2,"delete exactly selected real sheet and placeholder in one transaction");
   check(!((List<SheetInfo>)call("Read",new object[]{doc})).Any(s=>s.Number.StartsWith("MAN-")),"deleted sheets removed without deleting unrelated sheets");
   bool staleDelete=false;try{call("Delete",new object[]{doc,deletion});}catch(TargetInvocationException){staleDelete=true;}
   check(staleDelete,"stale sheet deletion rejected before modifying project");
   lines.Add("New test project closed without saving. Existing user project unchanged by tests.");
   File.WriteAllLines(Path.Combine(folder,"RevitTestResults.txt"),lines);
   TaskDialog.Show("Revit sheet integration tests",string.Join("\n",lines));return Result.Succeeded;
  }catch(Exception ex){lines.Add("FAIL: "+ex);File.WriteAllLines(Path.Combine(folder,"RevitTestResults.txt"),lines);message=ex.ToString();return Result.Failed;}
  finally{if(doc!=null && doc.IsValidObject)doc.Close(false);}
 }
}
