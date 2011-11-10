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

[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]

namespace iTextSharpExamples 
{

public class Simple
{

  public static void Main() 
  {
    try {
      string assemblyName= System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString()); 
      string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
      System.Random rnd= new System.Random(); 

      // step 1: create a document
      iTextSharp.text.Document document = new iTextSharp.text.Document();
      // step 2: we set the ContentType and create an instance of the Writer
      int pid= System.Diagnostics.Process.GetCurrentProcess().Id;
      //string Filename= System.String.Format("{0}-{1}-{2}.pdf", assemblyName, pid, rnd.Next(1000));
	  string Filename= "test.pdf";
      PdfWriter writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, 
																   new System.IO.FileStream(Filename, System.IO.FileMode.Create), new MyDocListener());

	  writer.PageEvent = new MyPdfPageEventHelper();
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
	  Chunk cc = new Chunk("foobar");
	  cc.SetLocalGoto("meins");
	  //cc.SetLocalDestination("fooooo");
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

	  Chunk cc2 = new Chunk("foobar2").SetLocalDestination("meins");
	  


	  document.Add(cc2);

	  
      // step 6: Close document

      document.Close();
      System.Console.Out.WriteLine("Hallo Welt");
	  
      System.Diagnostics.Process p; 
      p= new System.Diagnostics.Process();
      p.StartInfo.FileName= Filename;
      p.StartInfo.RedirectStandardOutput = false;
      p.StartInfo.UseShellExecute = true;

      p.Start();
    } 
    catch (iTextSharp.text.DocumentException ex) 
    {
     System.Console.Error.WriteLine(ex.StackTrace);
      System.Console.Error.WriteLine(ex.Message);
    }

    

  }
}


    public class MyPdfPageEventHelper : PdfPageEventHelper  {

        public  void OnStartPage(PdfWriter writer,Document document) {
				System.Console.Out.WriteLine("Document " );
				
        }
		
	}
	public class MyDocListener : IDocListener
	{

        public void Open()
			{
				
			}
    
        public void Close()
			{
				
			}

        public bool NewPage()
			{
				System.Console.Out.WriteLine("NewPage");
				return false;
			}
        public bool SetPageSize(Rectangle pageSize)
			{
				return true;
			}

        public bool SetMargins(float marginLeft, float marginRight, float marginTop, float marginBottom)
			{
				return true;
			}

		public bool SetMarginMirroring(bool marginMirroring)
			{
				return true;
			}

        public bool SetMarginMirroringTopBottom(bool marginMirroringTopBottom)
			{
				return true;
			}

        public int PageCount {
            set
				{
					
				}
        }
        public void ResetPageCount()
			{
				
			}
		
		public bool Add(IElement element)
			{
				System.Console.Out.WriteLine("Element " + element.Type);
				return true;
			}

		public void Dispose()
			{

			}
	}

}
