using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.util;
using iTextSharp.text.pdf.interfaces;
using iTextSharp.text.pdf.intern;
using iTextSharp.text.pdf.collection;
using iTextSharp.text.xml.xmp;
using iTextSharp.text.error_messages;
/*
 * This file is part of the iText project.
 * Copyright (c) 1998-2009 1T3XT BVBA
 * Authors: Bruno Lowagie, Paulo Soares, et al.
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
 * The interactive user interfaces in modified source and object code versions
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

namespace iTextSharp.text.pdf {
    public class PdfStamperImp : PdfWriter {
        internal Dictionary<PdfReader, IntHashtable> readers2intrefs = new Dictionary<PdfReader,IntHashtable>();
        internal Dictionary<PdfReader, RandomAccessFileOrArray> readers2file = new Dictionary<PdfReader,RandomAccessFileOrArray>();
        internal RandomAccessFileOrArray file;
        internal PdfReader reader;
        internal IntHashtable myXref = new IntHashtable();
        /** Integer(page number) -> PageStamp */
        internal Dictionary<PdfDictionary, PageStamp> pagesToContent = new Dictionary<PdfDictionary,PageStamp>();
        internal bool closed = false;
        /** Holds value of property rotateContents. */
        private bool rotateContents = true;
        protected AcroFields acroFields;
        protected bool flat = false;
        protected bool flatFreeText = false;
        protected int[] namePtr = {0};
        protected Dictionary<string,object> partialFlattening = new Dictionary<string,object>();
        protected bool useVp = false;
        protected PdfViewerPreferencesImp viewerPreferences = new PdfViewerPreferencesImp();
        protected Dictionary<PdfTemplate,object> fieldTemplates = new Dictionary<PdfTemplate,object>();
        protected bool fieldsAdded = false;
        protected int sigFlags = 0;
        protected internal bool append;
        protected IntHashtable marked;
        protected int initialXrefSize;
        protected PdfAction openAction;
        
        /** Creates new PdfStamperImp.
        * @param reader the read PDF
        * @param os the output destination
        * @param pdfVersion the new pdf version or '\0' to keep the same version as the original
        * document
        * @param append
        * @throws DocumentException on error
        * @throws IOException
        */
        internal PdfStamperImp(PdfReader reader, Stream os, char pdfVersion, bool append) : base(new PdfDocument(), os) {
            if (!reader.IsOpenedWithFullPermissions)
                throw new BadPasswordException(MessageLocalization.GetComposedMessage("pdfreader.not.opened.with.owner.password"));
            if (reader.Tampered)
                throw new DocumentException(MessageLocalization.GetComposedMessage("the.original.document.was.reused.read.it.again.from.file"));
            reader.Tampered = true;
            this.reader = reader;
            file = reader.SafeFile;
            this.append = append;
            if (append) {
                if (reader.IsRebuilt())
                    throw new DocumentException(MessageLocalization.GetComposedMessage("append.mode.requires.a.document.without.errors.even.if.recovery.was.possible"));
                if (reader.IsEncrypted())
                    crypto = new PdfEncryption(reader.Decrypt);
                pdf_version.SetAppendmode(true);
                file.ReOpen();
                byte[] buf = new byte[8192];
                int n;
                while ((n = file.Read(buf)) > 0)
                    this.os.Write(buf, 0, n);
                file.Close();
                prevxref = reader.LastXref;
                reader.Appendable = true;
            }
            else {
                if (pdfVersion == 0)
                    base.PdfVersion = reader.PdfVersion;
                else
                    base.PdfVersion = pdfVersion;
            }
            base.Open();
            pdf.AddWriter(this);
            if (append) {
                body.Refnum = reader.XrefSize;
                marked = new IntHashtable();
                if (reader.IsNewXrefType())
                    fullCompression = true;
                if (reader.IsHybridXref())
                    fullCompression = false;
            }
            initialXrefSize = reader.XrefSize;
        }
        
        internal void Close(Dictionary<String, String> moreInfo) {
            if (closed)
                return;
            if (useVp) {
                reader.SetViewerPreferences(viewerPreferences);
                MarkUsed(reader.Trailer.Get(PdfName.ROOT));
            }
            if (flat)
                FlatFields();
            if (flatFreeText)
                FlatFreeTextFields();
            AddFieldResources();
            PdfDictionary catalog = reader.Catalog;
            PdfDictionary pages = (PdfDictionary)PdfReader.GetPdfObject(catalog.Get(PdfName.PAGES));
            pages.Put(PdfName.ITXT, new PdfString(Document.Release));
            MarkUsed(pages);
            PdfDictionary acroForm = (PdfDictionary)PdfReader.GetPdfObject(catalog.Get(PdfName.ACROFORM), reader.Catalog);
            if (acroFields != null && acroFields.Xfa.Changed) {
                MarkUsed(acroForm);
                if (!flat)
                    acroFields.Xfa.SetXfa(this);
            }
            if (sigFlags != 0) {
                if (acroForm != null) {
                    acroForm.Put(PdfName.SIGFLAGS, new PdfNumber(sigFlags));
                    MarkUsed(acroForm);
                    MarkUsed(catalog);
                }
            }
            closed = true;
            AddSharedObjectsToBody();
            SetOutlines();
            SetJavaScript();
            AddFileAttachments();
            if (openAction != null) {
                catalog.Put(PdfName.OPENACTION, openAction);
            }
            if (pdf.pageLabels != null)
                catalog.Put(PdfName.PAGELABELS, pdf.pageLabels.GetDictionary(this));
            // OCG
            if (documentOCG.Count > 0) {
                FillOCProperties(false);
                PdfDictionary ocdict = catalog.GetAsDict(PdfName.OCPROPERTIES);
                if (ocdict == null) {
                    reader.Catalog.Put(PdfName.OCPROPERTIES, OCProperties);
                }
                else {
                    ocdict.Put(PdfName.OCGS, OCProperties.Get(PdfName.OCGS));
                    PdfDictionary ddict = ocdict.GetAsDict(PdfName.D);
                    if (ddict == null) {
                        ddict = new PdfDictionary();
                        ocdict.Put(PdfName.D, ddict);
                    }
                    ddict.Put(PdfName.ORDER, OCProperties.GetAsDict(PdfName.D).Get(PdfName.ORDER));
                    ddict.Put(PdfName.RBGROUPS, OCProperties.GetAsDict(PdfName.D).Get(PdfName.RBGROUPS));
                    ddict.Put(PdfName.OFF, OCProperties.GetAsDict(PdfName.D).Get(PdfName.OFF));
                    ddict.Put(PdfName.AS, OCProperties.GetAsDict(PdfName.D).Get(PdfName.AS));
                }
            }
            // metadata
            int skipInfo = -1;
            PRIndirectReference iInfo = (PRIndirectReference)reader.Trailer.Get(PdfName.INFO);
            PdfDictionary oldInfo = (PdfDictionary)PdfReader.GetPdfObject(iInfo);
            String producer = null;
            if (iInfo != null)
                skipInfo = iInfo.Number;
            if (oldInfo != null && oldInfo.Get(PdfName.PRODUCER) != null)
                producer = oldInfo.GetAsString(PdfName.PRODUCER).ToUnicodeString();
            if (producer == null) {
                producer = Document.Version;
            }
            else if (producer.IndexOf(Document.Product) == -1) {
                StringBuilder buf = new StringBuilder(producer);
                buf.Append("; modified using ");
                buf.Append(Document.Version);
                producer = buf.ToString();
            }
            // XMP
            byte[] altMetadata = null;
            PdfObject xmpo = PdfReader.GetPdfObject(catalog.Get(PdfName.METADATA));
            if (xmpo != null && xmpo.IsStream()) {
                altMetadata = PdfReader.GetStreamBytesRaw((PRStream)xmpo);
                PdfReader.KillIndirect(catalog.Get(PdfName.METADATA));
            }
            if (xmpMetadata != null) {
                altMetadata = xmpMetadata;
            }
            // if there is XMP data to add: add it
            PdfDate date = new PdfDate();
            if (altMetadata != null) {
                PdfStream xmp;
                try {
                    XmpReader xmpr = new XmpReader(altMetadata);
                    if (!(xmpr.ReplaceNode("http://ns.adobe.com/pdf/1.3/", "Producer", producer)
                        || xmpr.ReplaceDescriptionAttribute("http://ns.adobe.com/pdf/1.3/", "Producer", producer)))
                        xmpr.Add("rdf:Description", "http://ns.adobe.com/pdf/1.3/", "pdf:Producer", producer);
                    if (!(xmpr.ReplaceNode("http://ns.adobe.com/xap/1.0/", "ModifyDate", date.GetW3CDate())
                        || xmpr.ReplaceDescriptionAttribute("http://ns.adobe.com/xap/1.0/", "ModifyDate", date.GetW3CDate())))
                        xmpr.Add("rdf:Description", "http://ns.adobe.com/xap/1.0/", "xmp:ModifyDate", date.GetW3CDate());
                    if (!(xmpr.ReplaceNode("http://ns.adobe.com/xap/1.0/", "MetadataDate", date.GetW3CDate())
                            || xmpr.ReplaceDescriptionAttribute("http://ns.adobe.com/xap/1.0/", "MetadataDate", date.GetW3CDate()))) {
                    }
                    xmp = new PdfStream(xmpr.SerializeDoc());
                }
                catch {
                    xmp = new PdfStream(altMetadata);
                }
                xmp.Put(PdfName.TYPE, PdfName.METADATA);
                xmp.Put(PdfName.SUBTYPE, PdfName.XML);
                if (crypto != null && !crypto.IsMetadataEncrypted()) {
                    PdfArray ar = new PdfArray();
                    ar.Add(PdfName.CRYPT);
                    xmp.Put(PdfName.FILTER, ar);
                }
                if (append && xmpo != null) {
                    body.Add(xmp, xmpo.IndRef);
                }
                else {
                    catalog.Put(PdfName.METADATA, body.Add(xmp).IndirectReference);
                    MarkUsed(catalog);
                }
            }
            try {
                file.ReOpen();
                AlterContents();
                int rootN = ((PRIndirectReference)reader.trailer.Get(PdfName.ROOT)).Number;
                if (append) {
                    int[] keys = marked.GetKeys();
                    for (int k = 0; k < keys.Length; ++k) {
                        int j = keys[k];
                        PdfObject obj = reader.GetPdfObjectRelease(j);
                        if (obj != null && skipInfo != j && j < initialXrefSize) {
                            AddToBody(obj, j, j != rootN);
                        }
                    }
                    for (int k = initialXrefSize; k < reader.XrefSize; ++k) {
                        PdfObject obj = reader.GetPdfObject(k);
                        if (obj != null) {
                            AddToBody(obj, GetNewObjectNumber(reader, k, 0));
                        }
                    }
                }
                else {
                    for (int k = 1; k < reader.XrefSize; ++k) {
                        PdfObject obj = reader.GetPdfObjectRelease(k);
                        if (obj != null && skipInfo != k) {
                            AddToBody(obj, GetNewObjectNumber(reader, k, 0), k != rootN);
                        }
                    }
                }
            }
            finally {
                try {
                    file.Close();
                }
                catch  {
                    // empty on purpose
                }
            }
            PdfIndirectReference encryption = null;
            PdfObject fileID = null;
            if (crypto != null) {
                if (append) {
                    encryption = reader.GetCryptoRef();
                }
                else {
                    PdfIndirectObject encryptionObject = AddToBody(crypto.GetEncryptionDictionary(), false);
                    encryption = encryptionObject.IndirectReference;
                }
                fileID = crypto.FileID;
            }
            else
                fileID = PdfEncryption.CreateInfoId(PdfEncryption.CreateDocumentId());
            PRIndirectReference iRoot = (PRIndirectReference)reader.trailer.Get(PdfName.ROOT);
            PdfIndirectReference root = new PdfIndirectReference(0, GetNewObjectNumber(reader, iRoot.Number, 0));
            PdfIndirectReference info = null;
            PdfDictionary newInfo = new PdfDictionary();
            if (oldInfo != null) {
                foreach (PdfName key in oldInfo.Keys) {
                    PdfObject value = PdfReader.GetPdfObject(oldInfo.Get(key));
                    newInfo.Put(key, value);
                }
            }
            if (moreInfo != null) {
                foreach (KeyValuePair<string,string> entry in moreInfo) {
                    PdfName keyName = new PdfName(entry.Key);
                    String value = entry.Value;
                    if (value == null)
                        newInfo.Remove(keyName);
                    else
                        newInfo.Put(keyName, new PdfString(value, PdfObject.TEXT_UNICODE));
                }
            }
            newInfo.Put(PdfName.MODDATE, date);
            newInfo.Put(PdfName.PRODUCER, new PdfString(producer));
            if (append) {
                if (iInfo == null)
                    info = AddToBody(newInfo, false).IndirectReference;
                else
                    info = AddToBody(newInfo, iInfo.Number, false).IndirectReference;
            }
            else {
                info = AddToBody(newInfo, false).IndirectReference;
            }
            // write the cross-reference table of the body
            body.WriteCrossReferenceTable(os, root, info, encryption, fileID, prevxref);
            if (fullCompression) {
                byte[] tmp = GetISOBytes("startxref\n");
                os.Write(tmp, 0, tmp.Length);
                tmp = GetISOBytes(body.Offset.ToString());
                os.Write(tmp, 0, tmp.Length);
                tmp = GetISOBytes("\n%%EOF\n");
                os.Write(tmp, 0, tmp.Length);
            }
            else {
                PdfTrailer trailer = new PdfTrailer(body.Size,
                body.Offset,
                root,
                info,
                encryption,
                fileID, prevxref);
                trailer.ToPdf(this, os);
            }
            os.Flush();
            if (CloseStream)
                os.Close();
            reader.Close();
        }
        
        internal void ApplyRotation(PdfDictionary pageN, ByteBuffer out_p) {
            if (!rotateContents)
                return;
            Rectangle page = reader.GetPageSizeWithRotation(pageN);
            int rotation = page.Rotation;
            switch (rotation) {
                case 90:
                    out_p.Append(PdfContents.ROTATE90);
                    out_p.Append(page.Top);
                    out_p.Append(' ').Append('0').Append(PdfContents.ROTATEFINAL);
                    break;
                case 180:
                    out_p.Append(PdfContents.ROTATE180);
                    out_p.Append(page.Right);
                    out_p.Append(' ');
                    out_p.Append(page.Top);
                    out_p.Append(PdfContents.ROTATEFINAL);
                    break;
                case 270:
                    out_p.Append(PdfContents.ROTATE270);
                    out_p.Append('0').Append(' ');
                    out_p.Append(page.Right);
                    out_p.Append(PdfContents.ROTATEFINAL);
                    break;
            }
        }
        
        internal void AlterContents() {
            foreach (PageStamp ps in pagesToContent.Values) {
                PdfDictionary pageN = ps.pageN;
                MarkUsed(pageN);
                PdfArray ar = null;
                PdfObject content = PdfReader.GetPdfObject(pageN.Get(PdfName.CONTENTS), pageN);
                if (content == null) {
                    ar = new PdfArray();
                    pageN.Put(PdfName.CONTENTS, ar);
                }
                else if (content.IsArray()) {
                    ar = (PdfArray)content;
                    MarkUsed(ar);
                }
                else if (content.IsStream()) {
                    ar = new PdfArray();
                    ar.Add(pageN.Get(PdfName.CONTENTS));
                    pageN.Put(PdfName.CONTENTS, ar);
                }
                else {
                    ar = new PdfArray();
                    pageN.Put(PdfName.CONTENTS, ar);
                }
                ByteBuffer out_p = new ByteBuffer();
                if (ps.under != null) {
                    out_p.Append(PdfContents.SAVESTATE);
                    ApplyRotation(pageN, out_p);
                    out_p.Append(ps.under.InternalBuffer);
                    out_p.Append(PdfContents.RESTORESTATE);
                }
                if (ps.over != null)
                    out_p.Append(PdfContents.SAVESTATE);
                PdfStream stream = new PdfStream(out_p.ToByteArray());
                stream.FlateCompress(compressionLevel);
                ar.AddFirst(AddToBody(stream).IndirectReference);
                out_p.Reset();
                if (ps.over != null) {
                    out_p.Append(' ');
                    out_p.Append(PdfContents.RESTORESTATE);
                    ByteBuffer buf = ps.over.InternalBuffer;
                    out_p.Append(buf.Buffer, 0, ps.replacePoint);
                    out_p.Append(PdfContents.SAVESTATE);
                    ApplyRotation(pageN, out_p);
                    out_p.Append(buf.Buffer, ps.replacePoint, buf.Size - ps.replacePoint);
                    out_p.Append(PdfContents.RESTORESTATE);
                    stream = new PdfStream(out_p.ToByteArray());
                    stream.FlateCompress(compressionLevel);
                    ar.Add(AddToBody(stream).IndirectReference);
                }
                AlterResources(ps);
            }
        }

        internal void AlterResources(PageStamp ps) {
            ps.pageN.Put(PdfName.RESOURCES, ps.pageResources.Resources);
        }
        
        protected internal override int GetNewObjectNumber(PdfReader reader, int number, int generation) {
            IntHashtable ref_p;
            if (readers2intrefs.TryGetValue(reader, out ref_p)) {
                int n = ref_p[number];
                if (n == 0) {
                    n = IndirectReferenceNumber;
                    ref_p[number] = n;
                }
                return n;
            }
            if (currentPdfReaderInstance == null) {
                if (append && number < initialXrefSize)
                    return number;
                int n = myXref[number];
                if (n == 0) {
                    n = IndirectReferenceNumber;
                    myXref[number] = n;
                }
                return n;
            }
            else
                return currentPdfReaderInstance.GetNewObjectNumber(number, generation);
        }
        
        internal override RandomAccessFileOrArray GetReaderFile(PdfReader reader) {
            if (readers2intrefs.ContainsKey(reader)) {
                RandomAccessFileOrArray raf;
                if (readers2file.TryGetValue(reader, out raf))
                    return raf;
                return reader.SafeFile;
            }
            if (currentPdfReaderInstance == null)
                return file;
            else
                return currentPdfReaderInstance.ReaderFile;
        }
        
        /**
        * @param reader
        * @param openFile
        * @throws IOException
        */
        public void RegisterReader(PdfReader reader, bool openFile) {
            if (readers2intrefs.ContainsKey(reader))
                return;
            readers2intrefs[reader] = new IntHashtable();
            if (openFile) {
                RandomAccessFileOrArray raf = reader.SafeFile;
                readers2file[reader] = raf;
                raf.ReOpen();
            }
        }
        
        /**
        * @param reader
        */
        public void UnRegisterReader(PdfReader reader) {
            if (!readers2intrefs.ContainsKey(reader))
                return;
            readers2intrefs.Remove(reader);
            RandomAccessFileOrArray raf;
            if (!readers2file.TryGetValue(reader, out raf))
                return;
            readers2file.Remove(reader);
            try{raf.Close();}catch{}
        }

        internal static void FindAllObjects(PdfReader reader, PdfObject obj, IntHashtable hits) {
            if (obj == null)
                return;
            switch (obj.Type) {
                case PdfObject.INDIRECT:
                    PRIndirectReference iref = (PRIndirectReference)obj;
                    if (reader != iref.Reader)
                        return;
                    if (hits.ContainsKey(iref.Number))
                        return;
                    hits[iref.Number] = 1;
                    FindAllObjects(reader, PdfReader.GetPdfObject(obj), hits);
                    return;
                case PdfObject.ARRAY:
                    PdfArray a = (PdfArray)obj;
                    for (int k = 0; k < a.Size; ++k) {
                        FindAllObjects(reader, a[k], hits);
                    }
                    return;
                case PdfObject.DICTIONARY:
                case PdfObject.STREAM:
                    PdfDictionary dic = (PdfDictionary)obj;
                    foreach (PdfName name in dic.Keys) {
                        FindAllObjects(reader, dic.Get(name), hits);
                    }
                    return;
            }
        }
        
        /**
        * @param fdf
        * @throws IOException
        */
        public void AddComments(FdfReader fdf) {
            if (readers2intrefs.ContainsKey(fdf))
                return;
            PdfDictionary catalog = fdf.Catalog;
            catalog = catalog.GetAsDict(PdfName.FDF);
            if (catalog == null)
                return;
            PdfArray annots = catalog.GetAsArray(PdfName.ANNOTS);
            if (annots == null || annots.Size == 0)
                return;
            RegisterReader(fdf, false);
            IntHashtable hits = new IntHashtable();
            Dictionary<String, PdfObject> irt = new Dictionary<string,PdfObject>();
            List<PdfObject> an = new List<PdfObject>();
            for (int k = 0; k < annots.Size; ++k) {
                PdfObject obj = annots[k];
                PdfDictionary annot = (PdfDictionary)PdfReader.GetPdfObject(obj);
                PdfNumber page = annot.GetAsNumber(PdfName.PAGE);
                if (page == null || page.IntValue >= reader.NumberOfPages)
                    continue;
                FindAllObjects(fdf, obj, hits);
                an.Add(obj);
                if (obj.Type == PdfObject.INDIRECT) {
                    PdfObject nm = PdfReader.GetPdfObject(annot.Get(PdfName.NM));
                    if (nm != null && nm.Type == PdfObject.STRING)
                        irt[nm.ToString()] = obj;
                }
            }
            int[] arhits = hits.GetKeys();
            for (int k = 0; k < arhits.Length; ++k) {
                int n = arhits[k];
                PdfObject obj = fdf.GetPdfObject(n);
                if (obj.Type == PdfObject.DICTIONARY) {
                    PdfObject str = PdfReader.GetPdfObject(((PdfDictionary)obj).Get(PdfName.IRT));
                    if (str != null && str.Type == PdfObject.STRING) {
                        PdfObject i;
                        irt.TryGetValue(str.ToString(), out i);
                        if (i != null) {
                            PdfDictionary dic2 = new PdfDictionary();
                            dic2.Merge((PdfDictionary)obj);
                            dic2.Put(PdfName.IRT, i);
                            obj = dic2;
                        }
                    }
                }
                AddToBody(obj, GetNewObjectNumber(fdf, n, 0));
            }
            for (int k = 0; k < an.Count; ++k) {
                PdfObject obj = an[k];
                PdfDictionary annot = (PdfDictionary)PdfReader.GetPdfObject(obj);
                PdfNumber page = annot.GetAsNumber(PdfName.PAGE);
                PdfDictionary dic = reader.GetPageN(page.IntValue + 1);
                PdfArray annotsp = (PdfArray)PdfReader.GetPdfObject(dic.Get(PdfName.ANNOTS), dic);
                if (annotsp == null) {
                    annotsp = new PdfArray();
                    dic.Put(PdfName.ANNOTS, annotsp);
                    MarkUsed(dic);
                }
                MarkUsed(annotsp);
                annotsp.Add(obj);
            }
        }
        
        internal PageStamp GetPageStamp(int pageNum) {
            PdfDictionary pageN = reader.GetPageN(pageNum);
            PageStamp ps;
            pagesToContent.TryGetValue(pageN, out ps);
            if (ps == null) {
                ps = new PageStamp(this, reader, pageN);
                pagesToContent[pageN] = ps;
            }
            return ps;
        }
        
        internal PdfContentByte GetUnderContent(int pageNum) {
            if (pageNum < 1 || pageNum > reader.NumberOfPages)
                return null;
            PageStamp ps = GetPageStamp(pageNum);
            if (ps.under == null)
                ps.under = new StampContent(this, ps);
            return ps.under;
        }
        
        internal PdfContentByte GetOverContent(int pageNum) {
            if (pageNum < 1 || pageNum > reader.NumberOfPages)
                return null;
            PageStamp ps = GetPageStamp(pageNum);
            if (ps.over == null)
                ps.over = new StampContent(this, ps);
            return ps.over;
        }
        
        internal void CorrectAcroFieldPages(int page) {
            if (acroFields == null)
                return;
            if (page > reader.NumberOfPages)
                return;
            Dictionary<string,AcroFields.Item> fields = acroFields.Fields;
            foreach (AcroFields.Item item in fields.Values) {
                for (int k = 0; k < item.Size; ++k) {
                    int p = item.GetPage(k);
                    if (p >= page)
                        item.ForcePage(k, p + 1);
                }
            }
        }
        
        private static void MoveRectangle(PdfDictionary dic2, PdfReader r, int pageImported, PdfName key, String name) {
            Rectangle m = r.GetBoxSize(pageImported, name);
            if (m == null)
                dic2.Remove(key);
            else
                dic2.Put(key, new PdfRectangle(m));
        }
        
        internal void ReplacePage(PdfReader r, int pageImported, int pageReplaced) {
            PdfDictionary pageN = reader.GetPageN(pageReplaced);
            if (pagesToContent.ContainsKey(pageN))
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("this.page.cannot.be.replaced.new.content.was.already.added"));
            PdfImportedPage p = GetImportedPage(r, pageImported);
            PdfDictionary dic2 = reader.GetPageNRelease(pageReplaced);
            dic2.Remove(PdfName.RESOURCES);
            dic2.Remove(PdfName.CONTENTS);
            MoveRectangle(dic2, r, pageImported, PdfName.MEDIABOX, "media");
            MoveRectangle(dic2, r, pageImported, PdfName.CROPBOX, "crop");
            MoveRectangle(dic2, r, pageImported, PdfName.TRIMBOX, "trim");
            MoveRectangle(dic2, r, pageImported, PdfName.ARTBOX, "art");
            MoveRectangle(dic2, r, pageImported, PdfName.BLEEDBOX, "bleed");
            dic2.Put(PdfName.ROTATE, new PdfNumber(r.GetPageRotation(pageImported)));
            PdfContentByte cb = GetOverContent(pageReplaced);
            cb.AddTemplate(p, 0, 0);
            PageStamp ps = pagesToContent[pageN];
            ps.replacePoint = ps.over.InternalBuffer.Size;
        }

        internal void InsertPage(int pageNumber, Rectangle mediabox) {
            Rectangle media = new Rectangle(mediabox);
            int rotation = media.Rotation % 360;
            PdfDictionary page = new PdfDictionary(PdfName.PAGE);
            PdfDictionary resources = new PdfDictionary();
            PdfArray procset = new PdfArray();
            procset.Add(PdfName.PDF);
            procset.Add(PdfName.TEXT);
            procset.Add(PdfName.IMAGEB);
            procset.Add(PdfName.IMAGEC);
            procset.Add(PdfName.IMAGEI);
            resources.Put(PdfName.PROCSET, procset);
            page.Put(PdfName.RESOURCES, resources);
            page.Put(PdfName.ROTATE, new PdfNumber(rotation));
            page.Put(PdfName.MEDIABOX, new PdfRectangle(media, rotation));
            PRIndirectReference pref = reader.AddPdfObject(page);
            PdfDictionary parent;
            PRIndirectReference parentRef;
            if (pageNumber > reader.NumberOfPages) {
                PdfDictionary lastPage = reader.GetPageNRelease(reader.NumberOfPages);
                parentRef = (PRIndirectReference)lastPage.Get(PdfName.PARENT);
                parentRef = new PRIndirectReference(reader, parentRef.Number);
                parent = (PdfDictionary)PdfReader.GetPdfObject(parentRef);
                PdfArray kids = (PdfArray)PdfReader.GetPdfObject(parent.Get(PdfName.KIDS), parent);
                kids.Add(pref);
                MarkUsed(kids);
                reader.pageRefs.InsertPage(pageNumber, pref);
            }
            else {
                if (pageNumber < 1)
                    pageNumber = 1;
                PdfDictionary firstPage = reader.GetPageN(pageNumber);
                PRIndirectReference firstPageRef = reader.GetPageOrigRef(pageNumber);
                reader.ReleasePage(pageNumber);
                parentRef = (PRIndirectReference)firstPage.Get(PdfName.PARENT);
                parentRef = new PRIndirectReference(reader, parentRef.Number);
                parent = (PdfDictionary)PdfReader.GetPdfObject(parentRef);
                PdfArray kids = (PdfArray)PdfReader.GetPdfObject(parent.Get(PdfName.KIDS), parent);
                int len = kids.Size;
                int num = firstPageRef.Number;
                for (int k = 0; k < len; ++k) {
                    PRIndirectReference cur = (PRIndirectReference)kids[k];
                    if (num == cur.Number) {
                        kids.Add(k, pref);
                        break;
                    }
                }
                if (len == kids.Size)
                    throw new Exception(MessageLocalization.GetComposedMessage("internal.inconsistence"));
                MarkUsed(kids);
                reader.pageRefs.InsertPage(pageNumber, pref);
                CorrectAcroFieldPages(pageNumber);
            }
            page.Put(PdfName.PARENT, parentRef);
            while (parent != null) {
                MarkUsed(parent);
                PdfNumber count = (PdfNumber)PdfReader.GetPdfObjectRelease(parent.Get(PdfName.COUNT));
                parent.Put(PdfName.COUNT, new PdfNumber(count.IntValue + 1));
                parent = parent.GetAsDict(PdfName.PARENT);
            }
        }
        
        internal bool RotateContents {
            set {
                this.rotateContents = value;
            }
            get {
                return rotateContents;
            }
        }
        
        internal bool ContentWritten {
            get {
                return body.Size > 1;
            }
        }
        
        internal AcroFields AcroFields {
            get {
                if (acroFields == null) {
                    acroFields = new AcroFields(reader, this);
                }
                return acroFields;
            }
        }

        internal bool FormFlattening {
            set {
                flat = value;
            }
        }
        
        internal bool FreeTextFlattening {
            set {
                flatFreeText = value;
            }
        }
        
        internal bool PartialFormFlattening(String name) {
            AcroFields af = AcroFields;
            if (acroFields.Xfa.XfaPresent)
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("partial.form.flattening.is.not.supported.with.xfa.forms"));
            if (!acroFields.Fields.ContainsKey(name))
                return false;
            partialFlattening[name] = null;
            return true;
        }
        
        internal void FlatFields() {
            if (append)
                throw new ArgumentException(MessageLocalization.GetComposedMessage("field.flattening.is.not.supported.in.append.mode"));
            AcroFields af = AcroFields;
            Dictionary<string,AcroFields.Item> fields = acroFields.Fields;
            if (fieldsAdded && partialFlattening.Count == 0) {
                foreach (string obf in fields.Keys) {
                    partialFlattening[obf] = null;
                }
            }
            PdfDictionary acroForm = reader.Catalog.GetAsDict(PdfName.ACROFORM);
            PdfArray acroFds = null;
            if (acroForm != null) {
                acroFds = (PdfArray)PdfReader.GetPdfObject(acroForm.Get(PdfName.FIELDS), acroForm);
            }
            foreach (KeyValuePair<string,AcroFields.Item> entry in fields) {
                String name = entry.Key;
                if (partialFlattening.Count != 0 && !partialFlattening.ContainsKey(name))
                    continue;
                AcroFields.Item item = entry.Value;
                for (int k = 0; k < item.Size; ++k) {
                    PdfDictionary merged = item.GetMerged(k);
                    PdfNumber ff = merged.GetAsNumber(PdfName.F);
                    int flags = 0;
                    if (ff != null)
                        flags = ff.IntValue;
                    int page = item.GetPage(k);
                    PdfDictionary appDic = merged.GetAsDict(PdfName.AP);
                    if (appDic != null && (flags & PdfFormField.FLAGS_PRINT) != 0 && (flags & PdfFormField.FLAGS_HIDDEN) == 0) {
                        PdfObject obj = appDic.Get(PdfName.N);
                        PdfAppearance app = null;
                        if (obj != null) {
                            PdfObject objReal = PdfReader.GetPdfObject(obj);
                            if (obj is PdfIndirectReference && !obj.IsIndirect())
                                app = new PdfAppearance((PdfIndirectReference)obj);
                            else if (objReal is PdfStream) {
                                ((PdfDictionary)objReal).Put(PdfName.SUBTYPE, PdfName.FORM);
                                app = new PdfAppearance((PdfIndirectReference)obj);
                            }
                            else {
                                if (objReal != null && objReal.IsDictionary()) {
                                    PdfName as_p = merged.GetAsName(PdfName.AS);
                                    if (as_p != null) {
                                        PdfIndirectReference iref = (PdfIndirectReference)((PdfDictionary)objReal).Get(as_p);
                                        if (iref != null) {
                                            app = new PdfAppearance(iref);
                                            if (iref.IsIndirect()) {
                                                objReal = PdfReader.GetPdfObject(iref);
                                                ((PdfDictionary)objReal).Put(PdfName.SUBTYPE, PdfName.FORM);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (app != null) {
                            Rectangle box = PdfReader.GetNormalizedRectangle(merged.GetAsArray(PdfName.RECT));
                            PdfContentByte cb = GetOverContent(page);
                            cb.SetLiteral("Q ");
                            cb.AddTemplate(app, box.Left, box.Bottom);
                            cb.SetLiteral("q ");
                        }
                    }
                    if (partialFlattening.Count == 0)
                        continue;
                    PdfDictionary pageDic = reader.GetPageN(page);
                    PdfArray annots = pageDic.GetAsArray(PdfName.ANNOTS);
                    if (annots == null)
                        continue;
                    for (int idx = 0; idx < annots.Size; ++idx) {
                        PdfObject ran = annots[idx];
                        if (!ran.IsIndirect())
                            continue;
                        PdfObject ran2 = item.GetWidgetRef(k);
                        if (!ran2.IsIndirect())
                            continue;
                        if (((PRIndirectReference)ran).Number == ((PRIndirectReference)ran2).Number) {
                            annots.Remove(idx--);
                            PRIndirectReference wdref = (PRIndirectReference)ran2;
                            while (true) {
                                PdfDictionary wd = (PdfDictionary)PdfReader.GetPdfObject(wdref);
                                PRIndirectReference parentRef = (PRIndirectReference)wd.Get(PdfName.PARENT);
                                PdfReader.KillIndirect(wdref);
                                if (parentRef == null) { // reached AcroForm
                                    for (int fr = 0; fr < acroFds.Size; ++fr) {
                                        PdfObject h = acroFds[fr];
                                        if (h.IsIndirect() && ((PRIndirectReference)h).Number == wdref.Number) {
                                            acroFds.Remove(fr);
                                            --fr;
                                        }
                                    }
                                    break;
                                }
                                PdfDictionary parent = (PdfDictionary)PdfReader.GetPdfObject(parentRef);
                                PdfArray kids = parent.GetAsArray(PdfName.KIDS);
                                for (int fr = 0; fr < kids.Size; ++fr) {
                                    PdfObject h = kids[fr];
                                    if (h.IsIndirect() && ((PRIndirectReference)h).Number == wdref.Number) {
                                        kids.Remove(fr);
                                        --fr;
                                    }
                                }
                                if (!kids.IsEmpty())
                                    break;
                                wdref = parentRef;
                            }
                        }
                    }
                    if (annots.IsEmpty()) {
                        PdfReader.KillIndirect(pageDic.Get(PdfName.ANNOTS));
                        pageDic.Remove(PdfName.ANNOTS);
                    }
                }
            }
            if (!fieldsAdded && partialFlattening.Count == 0) {
                for (int page = 1; page <= reader.NumberOfPages; ++page) {
                    PdfDictionary pageDic = reader.GetPageN(page);
                    PdfArray annots = pageDic.GetAsArray(PdfName.ANNOTS);
                    if (annots == null)
                        continue;
                    for (int idx = 0; idx < annots.Size; ++idx) {
                        PdfObject annoto = annots.GetDirectObject(idx);
                        if ((annoto is PdfIndirectReference) && !annoto.IsIndirect())
                            continue;
                        if (!annoto.IsDictionary() || PdfName.WIDGET.Equals(((PdfDictionary)annoto).Get(PdfName.SUBTYPE))) {
                            annots.Remove(idx);
                            --idx;
                        }
                    }
                    if (annots.IsEmpty()) {
                        PdfReader.KillIndirect(pageDic.Get(PdfName.ANNOTS));
                        pageDic.Remove(PdfName.ANNOTS);
                    }
                }
                EliminateAcroformObjects();
            }
        }

        internal void EliminateAcroformObjects() {
            PdfObject acro = reader.Catalog.Get(PdfName.ACROFORM);
            if (acro == null)
                return;
            PdfDictionary acrodic = (PdfDictionary)PdfReader.GetPdfObject(acro);
            reader.KillXref(acrodic.Get(PdfName.XFA));
            acrodic.Remove(PdfName.XFA);
            PdfObject iFields = acrodic.Get(PdfName.FIELDS);
            if (iFields != null) {
                PdfDictionary kids = new PdfDictionary();
                kids.Put(PdfName.KIDS, iFields);
                SweepKids(kids);
                PdfReader.KillIndirect(iFields);
                acrodic.Put(PdfName.FIELDS, new PdfArray());
            }
            acrodic.Remove(PdfName.SIGFLAGS);
    //        PdfReader.KillIndirect(acro);
    //        reader.GetCatalog().Remove(PdfName.ACROFORM);
        }
        
        internal void SweepKids(PdfObject obj) {
            PdfObject oo = PdfReader.KillIndirect(obj);
            if (oo == null || !oo.IsDictionary())
                return;
            PdfDictionary dic = (PdfDictionary)oo;
            PdfArray kids = (PdfArray)PdfReader.KillIndirect(dic.Get(PdfName.KIDS));
            if (kids == null)
                return;
            for (int k = 0; k < kids.Size; ++k) {
                SweepKids(kids[k]);
            }
        }
        
        private void FlatFreeTextFields() {
            if (append)
                throw new ArgumentException(MessageLocalization.GetComposedMessage("freetext.flattening.is.not.supported.in.append.mode"));
            
            for (int page = 1; page <= reader.NumberOfPages; ++page) {
                PdfDictionary pageDic = reader.GetPageN(page);
                PdfArray annots = pageDic.GetAsArray(PdfName.ANNOTS);
                if (annots == null)
                    continue;
                for (int idx = 0; idx < annots.Size; ++idx) {
                    PdfObject annoto = annots.GetDirectObject(idx);
                    if ((annoto is PdfIndirectReference) && !annoto.IsIndirect())
                        continue;
                    
                    PdfDictionary annDic = (PdfDictionary)annoto;
                    if (!((PdfName)annDic.Get(PdfName.SUBTYPE)).Equals(PdfName.FREETEXT)) 
                        continue;
                    PdfNumber ff = annDic.GetAsNumber(PdfName.F);
                    int flags = (ff != null) ? ff.IntValue : 0;
                
                    if ( (flags & PdfFormField.FLAGS_PRINT) != 0 && (flags & PdfFormField.FLAGS_HIDDEN) == 0) {
                        PdfObject obj1 = annDic.Get(PdfName.AP);
                        if (obj1 == null) 
                            continue;
                        PdfDictionary appDic = (obj1 is PdfIndirectReference) ?
                            (PdfDictionary) PdfReader.GetPdfObject(obj1) : (PdfDictionary) obj1;         
                        PdfObject obj = appDic.Get(PdfName.N);
                        PdfAppearance app = null;
                        if (obj != null) {
                            PdfObject objReal = PdfReader.GetPdfObject(obj);
                            
                            if (obj is PdfIndirectReference && !obj.IsIndirect())
                                app = new PdfAppearance((PdfIndirectReference)obj);
                            else if (objReal is PdfStream) {
                                ((PdfDictionary)objReal).Put(PdfName.SUBTYPE, PdfName.FORM);
                                app = new PdfAppearance((PdfIndirectReference)obj);
                            }
                            else {
                                if (objReal.IsDictionary()) {
                                    PdfName as_p = appDic.GetAsName(PdfName.AS);
                                    if (as_p != null) {
                                        PdfIndirectReference iref = (PdfIndirectReference)((PdfDictionary)objReal).Get(as_p);
                                        if (iref != null) {
                                            app = new PdfAppearance(iref);
                                            if (iref.IsIndirect()) {
                                                objReal = PdfReader.GetPdfObject(iref);
                                                ((PdfDictionary)objReal).Put(PdfName.SUBTYPE, PdfName.FORM);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (app != null) {
                            Rectangle box = PdfReader.GetNormalizedRectangle(annDic.GetAsArray(PdfName.RECT));
                            PdfContentByte cb = this.GetOverContent(page);
                            cb.SetLiteral("Q ");
                            cb.AddTemplate(app, box.Left, box.Bottom);
                            cb.SetLiteral("q ");
                        }
                    }
                }
                for (int idx = 0; idx < annots.Size; ++idx) {
                    PdfDictionary annot = annots.GetAsDict(idx);
                    if (annot != null) {
                        if (PdfName.FREETEXT.Equals(annot.Get(PdfName.SUBTYPE))) {
                            annots.Remove(idx);
                            --idx;
                        }
                    }
                }
                if (annots.IsEmpty()) {
                    PdfReader.KillIndirect(pageDic.Get(PdfName.ANNOTS));
                    pageDic.Remove(PdfName.ANNOTS);
                }
            }
        }

        /**
        * @see com.lowagie.text.pdf.PdfWriter#getPageReference(int)
        */
        public override PdfIndirectReference GetPageReference(int page) {
            PdfIndirectReference ref_p = reader.GetPageOrigRef(page);
            if (ref_p == null)
                throw new ArgumentException(MessageLocalization.GetComposedMessage("invalid.page.number.1", page));
            return ref_p;
        }
        
        /**
        * @see com.lowagie.text.pdf.PdfWriter#addAnnotation(com.lowagie.text.pdf.PdfAnnotation)
        */
        public override void AddAnnotation(PdfAnnotation annot) {
            throw new Exception(MessageLocalization.GetComposedMessage("unsupported.in.this.context.use.pdfstamper.addannotation"));
        }
        
        internal void AddDocumentField(PdfIndirectReference ref_p) {
            PdfDictionary catalog = reader.Catalog;
            PdfDictionary acroForm = (PdfDictionary)PdfReader.GetPdfObject(catalog.Get(PdfName.ACROFORM), catalog);
            if (acroForm == null) {
                acroForm = new PdfDictionary();
                catalog.Put(PdfName.ACROFORM, acroForm);
                MarkUsed(catalog);
            }
            PdfArray fields = (PdfArray)PdfReader.GetPdfObject(acroForm.Get(PdfName.FIELDS), acroForm);
            if (fields == null) {
                fields = new PdfArray();
                acroForm.Put(PdfName.FIELDS, fields);
                MarkUsed(acroForm);
            }
            if (!acroForm.Contains(PdfName.DA)) {
                acroForm.Put(PdfName.DA, new PdfString("/Helv 0 Tf 0 g "));
                MarkUsed(acroForm);
            }
            fields.Add(ref_p);
            MarkUsed(fields);
        }
        
        internal void AddFieldResources() {
            if (fieldTemplates.Count == 0)
                return;
            PdfDictionary catalog = reader.Catalog;
            PdfDictionary acroForm = (PdfDictionary)PdfReader.GetPdfObject(catalog.Get(PdfName.ACROFORM), catalog);
            if (acroForm == null) {
                acroForm = new PdfDictionary();
                catalog.Put(PdfName.ACROFORM, acroForm);
                MarkUsed(catalog);
            }
            PdfDictionary dr = (PdfDictionary)PdfReader.GetPdfObject(acroForm.Get(PdfName.DR), acroForm);
            if (dr == null) {
                dr = new PdfDictionary();
                acroForm.Put(PdfName.DR, dr);
                MarkUsed(acroForm);
            }
            MarkUsed(dr);
            foreach (PdfTemplate template in fieldTemplates.Keys) {
                PdfFormField.MergeResources(dr, (PdfDictionary)template.Resources, this);
            }
//            if (dr.Get(PdfName.ENCODING) == null)
//                dr.Put(PdfName.ENCODING, PdfName.WIN_ANSI_ENCODING);
            PdfDictionary fonts = dr.GetAsDict(PdfName.FONT);
            if (fonts == null) {
                fonts = new PdfDictionary();
                dr.Put(PdfName.FONT, fonts);
            }
            if (!fonts.Contains(PdfName.HELV)) {
                PdfDictionary dic = new PdfDictionary(PdfName.FONT);
                dic.Put(PdfName.BASEFONT, PdfName.HELVETICA);
                dic.Put(PdfName.ENCODING, PdfName.WIN_ANSI_ENCODING);
                dic.Put(PdfName.NAME, PdfName.HELV);
                dic.Put(PdfName.SUBTYPE, PdfName.TYPE1);
                fonts.Put(PdfName.HELV, AddToBody(dic).IndirectReference);
            }
            if (!fonts.Contains(PdfName.ZADB)) {
                PdfDictionary dic = new PdfDictionary(PdfName.FONT);
                dic.Put(PdfName.BASEFONT, PdfName.ZAPFDINGBATS);
                dic.Put(PdfName.NAME, PdfName.ZADB);
                dic.Put(PdfName.SUBTYPE, PdfName.TYPE1);
                fonts.Put(PdfName.ZADB, AddToBody(dic).IndirectReference);
            }
            if (acroForm.Get(PdfName.DA) == null) {
                acroForm.Put(PdfName.DA, new PdfString("/Helv 0 Tf 0 g "));
                MarkUsed(acroForm);
            }
        }
        
        internal void ExpandFields(PdfFormField field, List<PdfAnnotation> allAnnots) {
            allAnnots.Add(field);
            List<PdfFormField> kids = field.Kids;
            if (kids != null) {
                for (int k = 0; k < kids.Count; ++k) {
                    ExpandFields((PdfFormField)kids[k], allAnnots);
                }
            }
        }

        internal void AddAnnotation(PdfAnnotation annot, PdfDictionary pageN) {
            List<PdfAnnotation> allAnnots = new List<PdfAnnotation>();
            if (annot.IsForm()) {
                fieldsAdded = true;
                AcroFields afdummy = AcroFields;
                PdfFormField field = (PdfFormField)annot;
                if (field.Parent != null)
                    return;
                ExpandFields(field, allAnnots);
            }
            else
                allAnnots.Add(annot);
            for (int k = 0; k < allAnnots.Count; ++k) {
                annot = (PdfAnnotation)allAnnots[k];
                if (annot.PlaceInPage > 0)
                    pageN = reader.GetPageN(annot.PlaceInPage);
                if (annot.IsForm()) {
                    if (!annot.IsUsed()) {
                        Dictionary<PdfTemplate,object> templates = annot.Templates;
                        if (templates != null) {
                            foreach (PdfTemplate tpl in templates.Keys) {
                                fieldTemplates[tpl] = null;
                            }
                        }
                    }
                    PdfFormField field = (PdfFormField)annot;
                    if (field.Parent == null)
                        AddDocumentField(field.IndirectReference);
                }
                if (annot.IsAnnotation()) {
                    PdfObject pdfobj = PdfReader.GetPdfObject(pageN.Get(PdfName.ANNOTS), pageN);
                    PdfArray annots = null;
                    if (pdfobj == null || !pdfobj.IsArray()) {
                        annots = new PdfArray();
                        pageN.Put(PdfName.ANNOTS, annots);
                        MarkUsed(pageN);
                    }
                    else 
                       annots = (PdfArray)pdfobj;
                    annots.Add(annot.IndirectReference);
                    MarkUsed(annots);
                    if (!annot.IsUsed()) {
                        PdfRectangle rect = (PdfRectangle)annot.Get(PdfName.RECT);
                        if (rect != null && (rect.Left != 0 || rect.Right != 0 || rect.Top != 0 || rect.Bottom != 0)) {
                            int rotation = reader.GetPageRotation(pageN);
                            Rectangle pageSize = reader.GetPageSizeWithRotation(pageN);
                            switch (rotation) {
                                case 90:
                                    annot.Put(PdfName.RECT, new PdfRectangle(
                                        pageSize.Top - rect.Top,
                                        rect.Right,
                                        pageSize.Top - rect.Bottom,
                                        rect.Left));
                                    break;
                                case 180:
                                    annot.Put(PdfName.RECT, new PdfRectangle(
                                        pageSize.Right - rect.Left,
                                        pageSize.Top - rect.Bottom,
                                        pageSize.Right - rect.Right,
                                        pageSize.Top - rect.Top));
                                    break;
                                case 270:
                                    annot.Put(PdfName.RECT, new PdfRectangle(
                                        rect.Bottom,
                                        pageSize.Right - rect.Left,
                                        rect.Top,
                                        pageSize.Right - rect.Right));
                                    break;
                            }
                        }
                    }
                }
                if (!annot.IsUsed()) {
                    annot.SetUsed();
                    AddToBody(annot, annot.IndirectReference);
                }
            }
        }
        
        internal override void AddAnnotation(PdfAnnotation annot, int page) {
            annot.Page = page;
            AddAnnotation(annot, reader.GetPageN(page));
        }

        private void OutlineTravel(PRIndirectReference outline) {
            while (outline != null) {
                PdfDictionary outlineR = (PdfDictionary)PdfReader.GetPdfObjectRelease(outline);
                PRIndirectReference first = (PRIndirectReference)outlineR.Get(PdfName.FIRST);
                if (first != null) {
                    OutlineTravel(first);
                }
                PdfReader.KillIndirect(outlineR.Get(PdfName.DEST));
                PdfReader.KillIndirect(outlineR.Get(PdfName.A));
                PdfReader.KillIndirect(outline);
                outline = (PRIndirectReference)outlineR.Get(PdfName.NEXT);
            }
        }

        internal void DeleteOutlines() {
            PdfDictionary catalog = reader.Catalog;
            PRIndirectReference outlines = (PRIndirectReference)catalog.Get(PdfName.OUTLINES);
            if (outlines == null)
                return;
            OutlineTravel(outlines);
            PdfReader.KillIndirect(outlines);
            catalog.Remove(PdfName.OUTLINES);
            MarkUsed(catalog);
        }
        
        internal void SetJavaScript() {
            Dictionary<string,PdfObject> djs = pdf.GetDocumentLevelJS();
            if (djs.Count == 0)
                return;
            PdfDictionary catalog = reader.Catalog;
            PdfDictionary names = (PdfDictionary)PdfReader.GetPdfObject(catalog.Get(PdfName.NAMES), catalog);
            if (names == null) {
                names = new PdfDictionary();
                catalog.Put(PdfName.NAMES, names);
                MarkUsed(catalog);
            }
            MarkUsed(names);
            PdfDictionary tree = PdfNameTree.WriteTree(djs, this);
            names.Put(PdfName.JAVASCRIPT, AddToBody(tree).IndirectReference);
        }
            
        void AddFileAttachments() {
            Dictionary<string,PdfObject> fs = pdf.GetDocumentFileAttachment();
            if (fs.Count == 0)
                return;
            PdfDictionary catalog = reader.Catalog;
            PdfDictionary names = (PdfDictionary)PdfReader.GetPdfObject(catalog.Get(PdfName.NAMES), catalog);
            if (names == null) {
                names = new PdfDictionary();
                catalog.Put(PdfName.NAMES, names);
                MarkUsed(catalog);
            }
            MarkUsed(names);
            Dictionary<string,PdfObject> old = PdfNameTree.ReadTree((PdfDictionary)PdfReader.GetPdfObjectRelease(names.Get(PdfName.EMBEDDEDFILES)));
            foreach (KeyValuePair<string,PdfObject> entry in fs) {
                String name = entry.Key;
                int k = 0;
                String nn = name;
                while (old.ContainsKey(nn)) {
                    ++k;
                    nn += " " + k;
                }
                old[nn] = entry.Value;
            }
            PdfDictionary tree = PdfNameTree.WriteTree(old, this);
            // Remove old EmbeddedFiles object if preset
            PdfObject oldEmbeddedFiles = names.Get(PdfName.EMBEDDEDFILES);
            if (oldEmbeddedFiles != null) {
                PdfReader.KillIndirect(oldEmbeddedFiles);
            }
			
            // Add new EmbeddedFiles object
            names.Put(PdfName.EMBEDDEDFILES, AddToBody(tree).IndirectReference);
        }

        /**
        * Adds or replaces the Collection Dictionary in the Catalog.
        * @param   collection  the new collection dictionary.
        */
        internal void MakePackage(PdfCollection collection) {
            PdfDictionary catalog = reader.Catalog;
            catalog.Put( PdfName.COLLECTION, collection );
        }

        internal void SetOutlines() {
            if (newBookmarks == null)
                return;
            DeleteOutlines();
            if (newBookmarks.Count == 0)
                return;
            PdfDictionary catalog = reader.Catalog;
            bool namedAsNames = (catalog.Get(PdfName.DESTS) != null);
            WriteOutlines(catalog, namedAsNames);
            MarkUsed(catalog);
        }
        
        /**
        * Sets the viewer preferences.
        * @param preferences the viewer preferences
        * @see PdfWriter#setViewerPreferences(int)
        */
        public override int ViewerPreferences {
            set {
                useVp = true;
                this.viewerPreferences.ViewerPreferences = value;
            }
        }
        
        /** Adds a viewer preference
        * @param preferences the viewer preferences
        * @see PdfViewerPreferences#addViewerPreference
        */
        public override void AddViewerPreference(PdfName key, PdfObject value) {
            useVp = true;
            this.viewerPreferences.AddViewerPreference(key, value);
        }

        /**
        * Set the signature flags.
        * @param f the flags. This flags are ORed with current ones
        */
        public override int SigFlags {
            set {
                sigFlags |= value;
            }
        }
        
        /** Always throws an <code>UnsupportedOperationException</code>.
        * @param actionType ignore
        * @param action ignore
        * @throws PdfException ignore
        * @see PdfStamper#setPageAction(PdfName, PdfAction, int)
        */    
        public override void SetPageAction(PdfName actionType, PdfAction action) {
            throw new InvalidOperationException(MessageLocalization.GetComposedMessage("use.setpageaction.pdfname.actiontype.pdfaction.action.int.page"));
        }

        /**
        * Sets the open and close page additional action.
        * @param actionType the action type. It can be <CODE>PdfWriter.PAGE_OPEN</CODE>
        * or <CODE>PdfWriter.PAGE_CLOSE</CODE>
        * @param action the action to perform
        * @param page the page where the action will be applied. The first page is 1
        * @throws PdfException if the action type is invalid
        */    
        internal void SetPageAction(PdfName actionType, PdfAction action, int page) {
            if (!actionType.Equals(PAGE_OPEN) && !actionType.Equals(PAGE_CLOSE))
                throw new PdfException(MessageLocalization.GetComposedMessage("invalid.page.additional.action.type.1", actionType.ToString()));
            PdfDictionary pg = reader.GetPageN(page);
            PdfDictionary aa = (PdfDictionary)PdfReader.GetPdfObject(pg.Get(PdfName.AA), pg);
            if (aa == null) {
                aa = new PdfDictionary();
                pg.Put(PdfName.AA, aa);
                MarkUsed(pg);
            }
            aa.Put(actionType, action);
            MarkUsed(aa);
        }

        /**
        * Always throws an <code>UnsupportedOperationException</code>.
        * @param seconds ignore
        */
        public override int Duration {
            set {
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("use.the.methods.at.pdfstamper"));
            }
        }
        
        /**
        * Always throws an <code>UnsupportedOperationException</code>.
        * @param transition ignore
        */
        public override PdfTransition Transition {
            set {
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("use.the.methods.at.pdfstamper"));
            }
        }

        /**
        * Sets the display duration for the page (for presentations)
        * @param seconds   the number of seconds to display the page. A negative value removes the entry
        * @param page the page where the duration will be applied. The first page is 1
        */
        internal void SetDuration(int seconds, int page) {
            PdfDictionary pg = reader.GetPageN(page);
            if (seconds < 0)
                pg.Remove(PdfName.DUR);
            else
                pg.Put(PdfName.DUR, new PdfNumber(seconds));
            MarkUsed(pg);
        }
        
        /**
        * Sets the transition for the page
        * @param transition   the transition object. A <code>null</code> removes the transition
        * @param page the page where the transition will be applied. The first page is 1
        */
        internal void SetTransition(PdfTransition transition, int page) {
            PdfDictionary pg = reader.GetPageN(page);
            if (transition == null)
                pg.Remove(PdfName.TRANS);
            else
                pg.Put(PdfName.TRANS, transition.TransitionDictionary);
            MarkUsed(pg);
        }

        protected internal void MarkUsed(PdfObject obj) {
            if (append && obj != null) {
                PRIndirectReference ref_p = null;
                if (obj.Type == PdfObject.INDIRECT)
                    ref_p = (PRIndirectReference)obj;
                else
                    ref_p = obj.IndRef;
                if (ref_p != null)
                    marked[ref_p.Number] = 1;
            }
        }
        
        protected internal void MarkUsed(int num) {
            if (append)
                marked[num] = 1;
        }
        
        /**
        * Getter for property append.
        * @return Value of property append.
        */
        internal bool IsAppend() {
            return append;
        }
            
        /** Additional-actions defining the actions to be taken in
        * response to various trigger events affecting the document
        * as a whole. The actions types allowed are: <CODE>DOCUMENT_CLOSE</CODE>,
        * <CODE>WILL_SAVE</CODE>, <CODE>DID_SAVE</CODE>, <CODE>WILL_PRINT</CODE>
        * and <CODE>DID_PRINT</CODE>.
        *
        * @param actionType the action type
        * @param action the action to execute in response to the trigger
        * @throws PdfException on invalid action type
        */
        public override void SetAdditionalAction(PdfName actionType, PdfAction action) {
            if (!(actionType.Equals(DOCUMENT_CLOSE) ||
            actionType.Equals(WILL_SAVE) ||
            actionType.Equals(DID_SAVE) ||
            actionType.Equals(WILL_PRINT) ||
            actionType.Equals(DID_PRINT))) {
                throw new PdfException(MessageLocalization.GetComposedMessage("invalid.additional.action.type.1", actionType.ToString()));
            }
            PdfDictionary aa = reader.Catalog.GetAsDict(PdfName.AA);
            if (aa == null) {
                if (action == null)
                    return;
                aa = new PdfDictionary();
                reader.Catalog.Put(PdfName.AA, aa);
            }
            MarkUsed(aa);
            if (action == null)
                aa.Remove(actionType);
            else
                aa.Put(actionType, action);
        }

        /**
        * @see com.lowagie.text.pdf.PdfWriter#setOpenAction(com.lowagie.text.pdf.PdfAction)
        */
        public override void SetOpenAction(PdfAction action) {
            openAction = action;
        }
        
        /**
        * @see com.lowagie.text.pdf.PdfWriter#setOpenAction(java.lang.String)
        */
        public override void SetOpenAction(String name) {
            throw new InvalidOperationException(MessageLocalization.GetComposedMessage("open.actions.by.name.are.not.supported"));
        }
        
        /**
        * @see com.lowagie.text.pdf.PdfWriter#setThumbnail(com.lowagie.text.Image)
        */
        public override Image Thumbnail {
            set {
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("use.pdfstamper.thumbnail"));
            }
        }
        
        internal void SetThumbnail(Image image, int page) {
            PdfIndirectReference thumb = GetImageReference(AddDirectImageSimple(image));
            reader.ResetReleasePage();
            PdfDictionary dic = reader.GetPageN(page);
            dic.Put(PdfName.THUMB, thumb);
            reader.ResetReleasePage();
        }
        
        /**
        * Reads the OCProperties dictionary from the catalog of the existing document
        * and fills the documentOCG, documentOCGorder and OCGRadioGroup variables in PdfWriter.
        * Note that the original OCProperties of the existing document can contain more information.
        * @since    2.1.2
        */
        protected void ReadOCProperties() {
            if (documentOCG.Count != 0) {
                return;
            }
            PdfDictionary dict = reader.Catalog.GetAsDict(PdfName.OCPROPERTIES);
            if (dict == null) {
                return;
            }
            PdfArray ocgs = dict.GetAsArray(PdfName.OCGS);
            PdfIndirectReference refi;
            PdfLayer layer;
            Dictionary<string,PdfLayer> ocgmap = new Dictionary<string,PdfLayer>();
            for (ListIterator<PdfObject> i = ocgs.GetListIterator(); i.HasNext();) {
                refi = (PdfIndirectReference)i.Next();
                layer = new PdfLayer(null);
                layer.Ref = refi;
                layer.OnPanel = false;
                layer.Merge((PdfDictionary)PdfReader.GetPdfObject(refi));
                ocgmap[refi.ToString()] = layer;
            }
            PdfDictionary d = dict.GetAsDict(PdfName.D);
            PdfArray off = d.GetAsArray(PdfName.OFF);
            if (off != null) {
                for (ListIterator<PdfObject> i = off.GetListIterator(); i.HasNext(); ) {
                    refi = (PdfIndirectReference)i.Next();
                    layer = ocgmap[refi.ToString()];
                    layer.On = false;
                }
            }
            PdfArray order = d.GetAsArray(PdfName.ORDER);
            if (order != null) {
                AddOrder(null, order, ocgmap);
            }
            foreach (PdfLayer o in ocgmap.Values)
                documentOCG[o] = null;
            OCGRadioGroup = d.GetAsArray(PdfName.RBGROUPS);
            OCGLocked = d.GetAsArray(PdfName.LOCKED);
            if (OCGLocked == null)
                OCGLocked = new PdfArray();
        }
        
        /**
        * Recursive method to reconstruct the documentOCGorder variable in the writer.
        * @param    parent  a parent PdfLayer (can be null)
        * @param    arr     an array possibly containing children for the parent PdfLayer
        * @param    ocgmap  a Hashtable with indirect reference Strings as keys and PdfLayer objects as values.
        * @since    2.1.2
        */
        private void AddOrder(PdfLayer parent, PdfArray arr, Dictionary<string,PdfLayer> ocgmap) {
            PdfObject obj;
            PdfLayer layer;
            for (int i = 0; i < arr.Size; i++) {
                obj = arr[i];
                if (obj.IsIndirect()) {
                    layer = ocgmap[obj.ToString()];
                    layer.OnPanel = true;
                    RegisterLayer(layer);
                    if (parent != null) {
                        parent.AddChild(layer);
                    }
                    if (arr.Size > i + 1 && arr[i + 1].IsArray()) {
                        i++;
                        AddOrder(layer, (PdfArray)arr[i], ocgmap);
                    }
                }
                else if (obj.IsArray()) {
                    PdfArray sub = (PdfArray)obj;
                    if (sub.IsEmpty()) return;
                    obj = sub[0];
                    if (obj.IsString()) {
                        layer = new PdfLayer(obj.ToString());
                        layer.OnPanel = true;
                        RegisterLayer(layer);
                        if (parent != null) {
                            parent.AddChild(layer);
                        }
                        PdfArray array = new PdfArray();
                        for (ListIterator<PdfObject> j = sub.GetListIterator(); j.HasNext(); ) {
                            array.Add(j.Next());
                        }
                        AddOrder(layer, array, ocgmap);
                    }
                    else {
                        AddOrder(parent, (PdfArray)obj, ocgmap);
                    }
                }
            }
        }
        
        /**
        * Gets the PdfLayer objects in an existing document as a Map
        * with the names/titles of the layers as keys.
        * @return   a Map with all the PdfLayers in the document (and the name/title of the layer as key)
        * @since    2.1.2
        */
        public Dictionary<string,PdfLayer> GetPdfLayers() {
            if (documentOCG.Count == 0) {
                ReadOCProperties();
            }
            Dictionary<string,PdfLayer> map = new Dictionary<string,PdfLayer>();
            String key;
            foreach (PdfLayer layer in documentOCG.Keys) {
                if (layer.Title == null) {
                    key = layer.GetAsString(PdfName.NAME).ToString();
                }
                else {
                    key = layer.Title;
                }
                if (map.ContainsKey(key)) {
                    int seq = 2;
                    String tmp = key + "(" + seq + ")";
                    while (map.ContainsKey(tmp)) {
                        seq++;
                        tmp = key + "(" + seq + ")";
                    }
                    key = tmp;
                }
                map[key] = layer;
            }
            return map;
        }
        
        internal class PageStamp {
            
            internal PdfDictionary pageN;
            internal StampContent under;
            internal StampContent over;
            internal PageResources pageResources;
            internal int replacePoint = 0;
            
            internal PageStamp(PdfStamperImp stamper, PdfReader reader, PdfDictionary pageN) {
                this.pageN = pageN;
                pageResources = new PageResources();
                PdfDictionary resources = pageN.GetAsDict(PdfName.RESOURCES);
                pageResources.SetOriginalResources(resources, stamper.namePtr);
            }
        }
    
        public override PdfContentByte DirectContent {
            get {
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("use.pdfstamper.getundercontent.or.pdfstamper.getovercontent"));
            }
        }
    
        public override PdfContentByte DirectContentUnder {
            get {
                throw new InvalidOperationException(MessageLocalization.GetComposedMessage("use.pdfstamper.getundercontent.or.pdfstamper.getovercontent"));
            }
        }
    }
}
