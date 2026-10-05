using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using MEP_Sheet_Manager;
class WorkbookTests {
 static void Check(bool value,string label) { if(!value) throw new Exception(label); Console.WriteLine("PASS: "+label); }
 static int Main(string[] args) {
  try {
   string folder=Path.GetFullPath(args[0]); Directory.CreateDirectory(folder); string file=Path.Combine(folder,"RoundTrip.xlsx");
   var rows=new List<SheetInfo>{new SheetInfo("001.01","Điện & nước <Tầng 1>",false),new SheetInfo("A-002","=Tên sheet dạng text",true),new SheetInfo("A-003","Điện & nước <Tầng 1>",false)};
   SheetWorkbook.Write(file,rows); var back=SheetWorkbook.Read(file);
   Check(back.Count==3,"row count");
   Check(back.Select(x=>x.Number).SequenceEqual(rows.Select(x=>x.Number)),"leading zeros and sheet numbers");
   Check(back.Select(x=>x.Name).SequenceEqual(rows.Select(x=>x.Name)),"Vietnamese, XML symbols, duplicate names, literal formula-like text");
   Check(back[1].IsPlaceholder && !back[0].IsPlaceholder,"placeholder round trip");
   SheetWorkbook.Write(file,rows); Check(SheetWorkbook.Read(file).Count==3,"atomic overwrite");
   string empty=Path.Combine(folder,"Empty.xlsx"); SheetWorkbook.Write(empty,new List<SheetInfo>()); Check(SheetWorkbook.Read(empty).Count==0,"empty workbook with headers");
   string shared=Path.Combine(folder,"SharedStrings.xlsx"); File.Copy(file,shared,true);
   using(var z=ZipFile.Open(shared,ZipArchiveMode.Update)) {
    using(var w=new StreamWriter(z.CreateEntry("xl/sharedStrings.xml").Open())) w.Write("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>0009</t></si></sst>");
    var e=z.GetEntry("xl/worksheets/sheet1.xml"); string xml; using(var r=new StreamReader(e.Open()))xml=r.ReadToEnd(); e.Delete();
    var d=System.Xml.Linq.XDocument.Parse(xml); System.Xml.Linq.XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var c=d.Descendants(ns+"c").First(x=>(string)x.Attribute("r")=="A2");c.RemoveNodes();c.SetAttributeValue("t","s");c.Add(new System.Xml.Linq.XElement(ns+"v","0"));
    using(var w=new StreamWriter(z.CreateEntry("xl/worksheets/sheet1.xml").Open()))w.Write(d.ToString());
   }
   Check(SheetWorkbook.Read(shared)[0].Number=="0009","Excel shared string input");
   string formula=Path.Combine(folder,"Formula.xlsx");File.Copy(file,formula,true);
   using(var z=ZipFile.Open(formula,ZipArchiveMode.Update)) {
    var e=z.GetEntry("xl/worksheets/sheet1.xml");string xml;using(var r=new StreamReader(e.Open()))xml=r.ReadToEnd();e.Delete();xml=xml.Replace("<is>","<f>1+1</f><is>");using(var w=new StreamWriter(z.CreateEntry("xl/worksheets/sheet1.xml").Open()))w.Write(xml);
   }
   bool rejected=false;try{SheetWorkbook.Read(formula);}catch(InvalidDataException){rejected=true;}Check(rejected,"reject formulas instead of stale cached values");
   LayoutTests.Run(folder); JsonTests.Run(folder); SheetUiTests.Run(); Console.WriteLine("All workbook, layout, JSON and filter tests passed.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}

