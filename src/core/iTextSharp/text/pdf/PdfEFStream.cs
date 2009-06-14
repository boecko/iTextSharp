using System;
using System.IO;
using System.Collections;
using System.util.zlib;
/*
 * $Id: PdfEFStream.java 3735 2009-02-26 01:44:03Z xlv $
 *
 * Copyright (c) 2008 by Bruno Lowagie
 *
 * The contents of this file are subject to the Mozilla Public License Version 1.1
 * (the "License"); you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the License.
 *
 * The Original Code is 'iText, a free JAVA-PDF library'.
 *
 * The Initial Developer of the Original Code is Bruno Lowagie. Portions created by
 * the Initial Developer are Copyright (C) 1999, 2000, 2001, 2002 by Bruno Lowagie.
 * All Rights Reserved.
 * Co-Developer of the code is Paulo Soares. Portions created by the Co-Developer
 * are Copyright (C) 2000, 2001, 2002 by Paulo Soares. All Rights Reserved.
 *
 * Contributor(s): all the names of the contributors are added in the source code
 * where applicable.
 *
 * Alternatively, the contents of this file may be used under the terms of the
 * LGPL license (the "GNU LIBRARY GENERAL PUBLIC LICENSE"), in which case the
 * provisions of LGPL are applicable instead of those above.  If you wish to
 * allow use of your version of this file only under the terms of the LGPL
 * License and not to allow others to use your version of this file under
 * the MPL, indicate your decision by deleting the provisions above and
 * replace them with the notice and other provisions required by the LGPL.
 * If you do not delete the provisions above, a recipient may use your version
 * of this file under either the MPL or the GNU LIBRARY GENERAL PUBLIC LICENSE.
 *
 * This library is free software; you can redistribute it and/or modify it
 * under the terms of the MPL as stated above or under the terms of the GNU
 * Library General Public License as published by the Free Software Foundation;
 * either version 2 of the License, or any later version.
 *
 * This library is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU Library general Public License for more
 * details.
 *
 * If you didn't download this code from the following link, you should check if
 * you aren't using an obsolete version:
 * http://www.lowagie.com/iText/
 */

namespace iTextSharp.text.pdf {

    /**
    * Extends PdfStream and should be used to create Streams for Embedded Files
    * (file attachments).
    * @since	2.1.3
    */

    public class PdfEFStream : PdfStream {

	    /**
	    * Creates a Stream object using an InputStream and a PdfWriter object
	    * @param	in	the InputStream that will be read to get the Stream object
	    * @param	writer	the writer to which the stream will be added
	    */
	    public PdfEFStream(Stream inp, PdfWriter writer) : base (inp, writer) {
	    }

	    /**
	    * Creates a Stream object using a byte array
	    * @param	fileStore	the bytes for the stream
	    */
	    public PdfEFStream(byte[] fileStore) : base(fileStore) {
	    }

        /**
        * @see com.lowagie.text.pdf.PdfDictionary#toPdf(com.lowagie.text.pdf.PdfWriter, java.io.OutputStream)
        */
        public override void ToPdf(PdfWriter writer, Stream os) {
            if (inputStream != null && compressed)
                Put(PdfName.FILTER, PdfName.FLATEDECODE);
            PdfEncryption crypto = null;
            if (writer != null)
                crypto = writer.Encryption;
            if (crypto != null) {
                PdfObject filter = Get(PdfName.FILTER);
                if (filter != null) {
                    if (PdfName.CRYPT.Equals(filter))
                        crypto = null;
                    else if (filter.IsArray()) {
                        PdfArray a = (PdfArray)filter;
                        if (!a.IsEmpty() && PdfName.CRYPT.Equals(a[0]))
                            crypto = null;
                    }
                }
            }
    	    if (crypto != null && crypto.IsEmbeddedFilesOnly()) {
    		    PdfArray filter = new PdfArray();
    		    PdfArray decodeparms = new PdfArray();
    		    PdfDictionary crypt = new PdfDictionary();
    		    crypt.Put(PdfName.NAME, PdfName.STDCF);
    		    filter.Add(PdfName.CRYPT);
    		    decodeparms.Add(crypt);
    		    if (compressed) {
    			    filter.Add(PdfName.FLATEDECODE);
    			    decodeparms.Add(new PdfNull());
    		    }
    		    Put(PdfName.FILTER, filter);
    		    Put(PdfName.DECODEPARMS, decodeparms);
    	    }
            PdfObject nn = Get(PdfName.LENGTH);
            if (crypto != null && nn != null && nn.IsNumber()) {
                int sz = ((PdfNumber)nn).IntValue;
                Put(PdfName.LENGTH, new PdfNumber(crypto.CalculateStreamSize(sz)));
                SuperToPdf(writer, os);
                Put(PdfName.LENGTH, nn);
            }
            else
                SuperToPdf(writer, os);

            os.Write(STARTSTREAM, 0, STARTSTREAM.Length);
            if (inputStream != null) {
                rawLength = 0;
                ZDeflaterOutputStream def = null;
                OutputStreamCounter osc = new OutputStreamCounter(os);
                OutputStreamEncryption ose = null;
                Stream fout = osc;
                if (crypto != null)
                    fout = ose = crypto.GetEncryptionStream(fout);
                if (compressed)    
                    fout = def = new ZDeflaterOutputStream(fout, compressionLevel);
                
                byte[] buf = new byte[4192];
                while (true) {
                    int n = inputStream.Read(buf, 0, buf.Length);
                    if (n <= 0)
                        break;
                    fout.Write(buf, 0, n);
                    rawLength += n;
                }
                if (def != null)
                    def.Finish();
                if (ose != null)
                    ose.Finish();
                inputStreamLength = osc.Counter;
            }
            else {
                if (crypto == null) {
                    if (streamBytes != null)
                        streamBytes.WriteTo(os);
                    else
                        os.Write(bytes, 0, bytes.Length);
                }
                else {
                    byte[] b;
                    if (streamBytes != null) {
                        b = crypto.EncryptByteArray(streamBytes.ToArray());
                    }
                    else {
                        b = crypto.EncryptByteArray(bytes);
                    }
                    os.Write(b, 0, b.Length);
                }
            }
            os.Write(ENDSTREAM, 0, ENDSTREAM.Length);
        }
    }
}