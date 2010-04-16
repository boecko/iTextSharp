using System;
using System.IO;
using iTextSharp.text.pdf;
using iTextSharp.text.xml.simpleparser;
/*
 * $Id: ContentOperator.java 4242 2010-01-02 23:22:20Z xlv $
 *
 * This file is part of the iText project.
 * Copyright (c) 1998-2009 1T3XT BVBA
 * Authors: Bruno Lowagie, et al.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License version 3
 * as published by the Free Software Foundation with the addition of the
 * following permission added to Section 15 as permitted in Section 7(a):
 * FOR ANY PART OF THE COVERED WORK IN WHICH THE COPYRIGHT IS OWNED BY 1T3XT,
 * 1T3XT DISCLAIMS THE WARRANTY OF NON INFRINGEMENT OF THIRD PARTY RIGHTS.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU Affero General Public License for more details.
 * You should have received a copy of the GNU Affero General Public License
 * along with this program; if not, see http://www.gnu.org/licenses or write to
 * the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
 * Boston, MA, 02110-1301 USA, or download the license from the following URL:
 * http://itextpdf.com/terms-of-use/
 *
 * The interactive user interfaces in modified source and obj code versions
 * of this program must display Appropriate Legal Notices, as required under
 * Section 5 of the GNU Affero General Public License.
 *
 * In accordance with Section 7(b) of the GNU Affero General Public License,
 * you must retain the producer line in every PDF that is created or manipulated
 * using iText.
 *
 * You can be released from the requirements of the license by purchasing
 * a commercial license. Buying such a license is mandatory as soon as you
 * develop commercial activities involving the iText software without
 * disclosing the source code of your own applications.
 * These activities include: offering paid services to customers as an ASP,
 * serving PDFs on the fly in a web application, shipping iText with a closed
 * source product.
 *
 * For more information, please contact iText Software Corp. at this
 * address: sales@itextpdf.com
 */
namespace iTextSharp.text.pdf.parser {

    /**
     * Converts a tagged PDF document into an XML file.
     * 
     * @since 5.0.2
     */
    public class TaggedPdfReaderTool {

        /** The reader obj from which the content streams are read. */
        PdfReader reader;
        /** The writer obj to which the XML will be written */
        StreamWriter outp;

        /**
         * Parses a string with structured content.
         * 
         * @param reader
         *            the PdfReader that has access to the PDF file
         * @param os
         *            the Stream to which the resulting xml will be written
         */
        public void ConvertToXml(PdfReader reader, Stream os) {
            this.reader = reader;
            outp = new StreamWriter(os);
            // get the StructTreeRoot from the root obj
            PdfDictionary catalog = reader.Catalog;
            PdfDictionary struc = catalog.GetAsDict(PdfName.STRUCTTREEROOT);
            // Inspect the child or children of the StructTreeRoot
            InspectChild(struc.GetDirectObject(PdfName.K));
            outp.Flush();
            outp.Close();
        }

        /**
         * Inspects a child of a structured element. This can be an array or a
         * dictionary.
         * 
         * @param k
         *            the child to inspect
         * @throws IOException
         */
        public void InspectChild(PdfObject k) {
            if (k == null)
                return;
            if (k is PdfArray)
                InspectChildArray((PdfArray) k);
            else if (k is PdfDictionary)
                InspectChildDictionary((PdfDictionary) k);
        }

        /**
         * If the child of a structured element is an array, we need to loop over
         * the elements.
         * 
         * @param k
         *            the child array to inspect
         */
        public void InspectChildArray(PdfArray k) {
            if (k == null)
                return;
            for (int i = 0; i < k.Size; i++) {
                InspectChild(k.GetDirectObject(i));
            }
        }

        /**
         * If the child of a structured element is a dictionary, we inspect the
         * child; we may also draw a tag.
         * 
         * @param k
         *            the child dictionary to inspect
         */
        public void InspectChildDictionary(PdfDictionary k) {
            if (k == null)
                return;
            PdfName s = k.GetAsName(PdfName.S);
            if (s != null) {
                String tag = s.ToString().Substring(1);
                outp.Write("<");
                outp.Write(tag);
                outp.Write(">");
                PdfDictionary dict = k.GetAsDict(PdfName.PG);
                if (dict != null)
                    ParseTag(tag, k.GetDirectObject(PdfName.K), dict);
                InspectChild(k.Get(PdfName.K));
                outp.Write("</");
                outp.Write(tag);
                outp.WriteLine(">");
            } else
                InspectChild(k.Get(PdfName.K));
        }

        /**
         * Searches for a tag in a page.
         * 
         * @param tag
         *            the name of the tag
         * @param obj
         *            an identifier to find the marked content
         * @param page
         *            a page dictionary
         * @throws IOException
         */
        public void ParseTag(String tag, PdfObject obj, PdfDictionary page) {
            PRStream stream = (PRStream) page.GetAsStream(PdfName.CONTENTS);
            // if the identifier is a number, we can extract the content right away
            if (obj is PdfNumber) {
                PdfNumber mcid = (PdfNumber) obj;
                RenderFilter filter = new MarkedContentRenderFilter(mcid.IntValue);
                ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                FilteredTextRenderListener listener = new FilteredTextRenderListener(strategy, new RenderFilter[]{filter});
                PdfContentStreamProcessor processor = new PdfContentStreamProcessor(
                        listener);
                processor.ProcessContent(PdfReader.GetStreamBytes(stream), page
                        .GetAsDict(PdfName.RESOURCES));
                outp.Write(SimpleXMLParser.EscapeXML(listener.GetResultantText(), true));
            }
            // if the identifier is an array, we call the parseTag method
            // recursively
            else if (obj is PdfArray) {
                PdfArray arr = (PdfArray) obj;
                int n = arr.Size;
                for (int i = 0; i < n; i++) {
                    ParseTag(tag, arr[i], page);
                    if (i < n - 1)
                        outp.WriteLine();
                }
            }
            // if the identifier is a dictionary, we get the resources from the
            // dictionary
            else if (obj is PdfDictionary) {
                PdfDictionary mcr = (PdfDictionary) obj;
                ParseTag(tag, mcr.GetDirectObject(PdfName.MCID), mcr
                        .GetAsDict(PdfName.PG));
            }
        }
    }
}