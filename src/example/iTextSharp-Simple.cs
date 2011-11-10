//
// Itextsharp-Simple.cs
// 
// Simple example of how to use itextsharp library (v3.0.10.0) to dynamically generate a simple PDF. 
// For iTextSharp, see http://itextsharp.sourceforge.net 
//
// compile with: 
// 
//     csc /target:exe /r:itextsharp.dll iTextSharp-Simple.cs
//
// Thu, 02 Feb 2006  09:15
//
// -------------------------------------------------------

using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.IO;
using System.Collections.Generic;

namespace iTextSharpExamples 
{

public class Simple
{

  public static void Main() 
  {


	  string Filename= "test.pdf";
	  Dictionary<string,int> dict = new Dictionary<string, int>();
	  //dict["fop"] = 1;
	  Stream fileStream ;
	  Console.Out.WriteLine("Eintraege: " + dict.Values.Count);
	  fileStream = new System.IO.FileStream("foo.pdf", System.IO.FileMode.Create);
	  doPDF(fileStream, dict);

	  Console.Out.WriteLine("Eintraege: " + dict.Values.Count);
	  fileStream = new System.IO.FileStream(Filename, System.IO.FileMode.Create);
	  doPDF(fileStream, dict);

      System.Diagnostics.Process p; 
      p= new System.Diagnostics.Process();
      p.StartInfo.FileName= Filename;
      p.StartInfo.RedirectStandardOutput = false;
      p.StartInfo.UseShellExecute = true;
      p.Start();

  }
	public  static void doPDF(Stream stream, Dictionary<string, int> dict)
  {
   try {
      string assemblyName= System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString()); 
      string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

      // step 1: create a document
      iTextSharp.text.Document document = new iTextSharp.text.Document();
      // step 2: we set the ContentType and create an instance of the Writer
      PdfWriter writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, stream); 

	  writer.PageEvent = new MyPdfPageEventHelper(dict);
	  /*
      PdfWriter writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, 
			    new System.IO.FileStream(Filename, System.IO.FileMode.Create));
				*/
      // step 3:  add metadata (before document.Open())
      document.AddTitle("Sample Document");
      document.AddSubject("Csharp PDF creation example");
      document.AddKeywords("csharp dotnet examples");
      document.AddCreator(".NET Assembly: " + assemblyName);
      document.AddAuthor("Dino Chiesa");
      document.AddProducer();
            
      // step 4: open the doc
      document.Open();

      // step 5: we Add content to the document
      //TODO: increase font size
      iTextSharp.text.Font font24= iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 24);
      iTextSharp.text.Font font18= iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 18);
      iTextSharp.text.Font fontAnchor= iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10, 
									   iTextSharp.text.Font.UNDERLINE, 
									   new iTextSharp.text.BaseColor(0, 0, 255));
      iTextSharp.text.Chunk bullet= new iTextSharp.text.Chunk("\u2022", font18);

      document.Add(new iTextSharp.text.Paragraph(new iTextSharp.text.Chunk("Hello, World! ", font24))); 

      document.Add(new iTextSharp.text.Paragraph("\n"));
      document.Add(new iTextSharp.text.Paragraph("This PDF document was generated dynamically: "));
      iTextSharp.text.List list= new iTextSharp.text.List(false, 20);  // true= ordered, false= unordered
	  Chunk cc = CreateLocalGotoToChunk("\n\nSpringt zu Seite $page\n\n", "marker_page_2", dict);

	  document.Add(cc);

	  cc = CreateLocalGotoToChunk("\n\nSpringt zu Seite $page\n\n", "marker_page_4", dict);

	  document.Add(cc);
      list.ListSymbol = bullet;       // use "bullet" as list symbol
      list.Add(new iTextSharp.text.ListItem("on " + System.DateTime.Now.ToString("dddd, MMM d, yyyy")));
      list.Add(new iTextSharp.text.ListItem("at " + System.DateTime.Now.ToString("hh:mm:ss tt zzzz")));
      list.Add(new iTextSharp.text.ListItem("on machine " + System.Environment.MachineName));
      list.Add(new iTextSharp.text.ListItem("by .NET assembly: " + assemblyName + " " + assemblyVersion));
      list.Add(new iTextSharp.text.ListItem("on a machine running " + System.Environment.OSVersion.ToString()));
      list.Add(new iTextSharp.text.ListItem("and .NET CLR " + System.Environment.Version));


      string v1= "(none)"; 
      string v2= "(none)";
      try {
		  v1=  list.GetType().Assembly.GetName().Version.ToString();
		  v2=  list.GetType().Assembly.ImageRuntimeVersion;
      }
      catch (System.Exception e1) {v1 = e1.ToString();}
      
      iTextSharp.text.ListItem li=  new iTextSharp.text.ListItem(System.String.Format("iTextSharp v{0} (compiled with .NET {1}) see ", v1,v2));
      iTextSharp.text.Anchor anchor = 
		  new iTextSharp.text.Anchor("http://itextsharp.sourceforge.net/", fontAnchor); 
      anchor.Reference = "http://itextsharp.sourceforge.net";
      //anchor.Name = "website"; 

      li.Add(anchor);
      list.Add(li);

      
      document.Add(list);
	  document.NewPage();

	  Chunk cc2 = new Chunk("Ich bin auf Seite 2").SetLocalDestination("marker_page_2");
	  


	  document.Add(cc2);
	  document.NewPage();
	  document.Add(new Paragraph("\n"));
	  document.NewPage();

	  Chunk cc3 = new Chunk("Ich bin auf Seite 4").SetLocalDestination("marker_page_4");
	  

	  document.Add(cc3);

	  
      // step 6: Close document

      document.Close();
	  
    } 
    catch (iTextSharp.text.DocumentException ex) 
    {
     System.Console.Error.WriteLine(ex.StackTrace);
      System.Console.Error.WriteLine(ex.Message);
    }

    

  }

   static Chunk CreateLocalGotoToChunk(string text, string marker, Dictionary<string,int> dict)
   {
	  if(dict.ContainsKey(marker))
		  {
			  Console.Out.WriteLine("foobar");
			  text = text.Replace("$page", dict[marker].ToString());
		  }
	  Chunk cc = new Chunk(text);
	  cc.SetLocalGoto(marker);
	  return cc;
   }
}


    public class MyPdfPageEventHelper : PdfPageEventHelper  {

		Dictionary<string,int> dict = null;
		public  MyPdfPageEventHelper(Dictionary<string,int> dict)
			{
				
				this.dict = dict;
			}
        public override void OnStartPage(PdfWriter writer,Document document) {
			//System.Console.Out.WriteLine("OnStartPage " );
				
        }

        public override void OnLocalDestination(PdfWriter writer, Document document, PdfDestination dest, string marker) {
			Console.Out.WriteLine("OnLocalDestination text:" + marker + " writer.CurrentPageNumber: " + writer.CurrentPageNumber );
			dict[marker] = writer.CurrentPageNumber;
        }

		
	}

}
