using System;

using iTextSharp.text;

/*
 * $Id: PdfImportedPage.cs,v 1.3 2008/05/13 11:25:21 psoares33 Exp $
 * 
 *
 * Copyright 2001, 2002 Paulo Soares
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

    /** Represents an imported page.
     *
     * @author Paulo Soares (psoares@consiste.pt)
     */
    public class PdfImportedPage : PdfTemplate {

        internal PdfReaderInstance readerInstance;
        internal int pageNumber;
    
        internal PdfImportedPage(PdfReaderInstance readerInstance, PdfWriter writer, int pageNumber) {
            this.readerInstance = readerInstance;
            this.pageNumber = pageNumber;
            thisReference = writer.PdfIndirectReference;
            bBox = readerInstance.Reader.GetPageSize(pageNumber);
            SetMatrix(1, 0, 0, 1, -bBox.Left, -bBox.Bottom);
            type = TYPE_IMPORTED;
        }

        /** Reads the content from this <CODE>PdfImportedPage</CODE>-object from a reader.
        *
        * @return self
        *
        */
        public PdfImportedPage FromReader {
            get {
                return this;
            }
        }

        public int PageNumber {
            get {
                return pageNumber;
            }
        }

        /** Always throws an error. This operation is not allowed.
         * @param image dummy
         * @param a dummy
         * @param b dummy
         * @param c dummy
         * @param d dummy
         * @param e dummy
         * @param f dummy
         * @throws DocumentException  dummy */    
        public override void AddImage(Image image, float a, float b, float c, float d, float e, float f) {
            ThrowError();
        }
    
        /** Always throws an error. This operation is not allowed.
         * @param template dummy
         * @param a dummy
         * @param b dummy
         * @param c dummy
         * @param d dummy
         * @param e dummy
         * @param f  dummy */    
        public override void AddTemplate(PdfTemplate template, float a, float b, float c, float d, float e, float f) {
            ThrowError();
        }
    
        /** Always throws an error. This operation is not allowed.
         * @return  dummy */    
        public override PdfContentByte Duplicate {
            get {
                ThrowError();
                return null;
            }
        }
    
        /**
        * Gets the stream representing this page.
        *
        * @param   compressionLevel    the compressionLevel
        * @return the stream representing this page
        * @since   2.1.3   (replacing the method without param compressionLevel)
        */
        internal override PdfStream GetFormXObject(int compressionLevel) {
            return readerInstance.GetFormXObject(pageNumber, compressionLevel);
        }
    
        public override void SetColorFill(PdfSpotColor sp, float tint) {
            ThrowError();
        }
    
        public override void SetColorStroke(PdfSpotColor sp, float tint) {
            ThrowError();
        }
    
        internal override PdfObject Resources {
            get {
                return readerInstance.GetResources(pageNumber);
            }
        }
    
        /** Always throws an error. This operation is not allowed.
         * @param bf dummy
         * @param size dummy */    
        public override void SetFontAndSize(BaseFont bf, float size) {
            ThrowError();
        }
    
        internal void ThrowError() {
            throw new Exception("Content can not be added to a PdfImportedPage.");
        }
    
        internal PdfReaderInstance PdfReaderInstance {
            get {
                return readerInstance;
            }
        }
    }
}