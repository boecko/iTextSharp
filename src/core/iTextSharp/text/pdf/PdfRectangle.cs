using System;

using iTextSharp.text;

/*
 * Copyright 1999, 2000, 2001, 2002 Bruno Lowagie
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
     * <CODE>PdfRectangle</CODE> is the PDF Rectangle object.
     * <P>
     * Rectangles are used to describe locations on the page and bounding boxes for several
     * objects in PDF, such as fonts. A rectangle is represented as an <CODE>array</CODE> of
     * four numbers, specifying the lower lef <I>x</I>, lower left <I>y</I>, upper right <I>x</I>,
     * and upper right <I>y</I> coordinates of the rectangle, in that order.<BR>
     * This object is described in the 'Portable Document Format Reference Manual version 1.3'
     * section 7.1 (page 183).
     *
     * @see     iTextSharp.text.Rectangle
     * @see     PdfArray
     */

    public class PdfRectangle : PdfArray {
    
        // membervariables
    
        /** lower left x */
        private float llx = 0;
    
        /** lower left y */
        private float lly = 0;
    
        /** upper right x */
        private float urx = 0;
    
        /** upper right y */
        private float ury = 0;
    
        // constructors
    
        /**
         * Constructs a <CODE>PdfRectangle</CODE>-object.
         *
         * @param       llx         lower left x
         * @param       lly         lower left y
         * @param       urx         upper right x
         * @param       ury         upper right y
         *
         * @since       rugPdf0.10
         */
    
        public PdfRectangle(float llx, float lly, float urx, float ury, int rotation) : base() {
            if (rotation == 90 || rotation == 270) {
                this.llx = lly;
                this.lly = llx;
                this.urx = ury;
                this.ury = urx;
            }
            else {
                this.llx = llx;
                this.lly = lly;
                this.urx = urx;
                this.ury = ury;
            }
            base.Add(new PdfNumber(this.llx));
            base.Add(new PdfNumber(this.lly));
            base.Add(new PdfNumber(this.urx));
            base.Add(new PdfNumber(this.ury));
        }

        public PdfRectangle(float llx, float lly, float urx, float ury) : this(llx, lly, urx, ury, 0) {}

        /**
         * Constructs a <CODE>PdfRectangle</CODE>-object starting from the origin (0, 0).
         *
         * @param       urx         upper right x
         * @param       ury         upper right y
         */
    
        public PdfRectangle(float urx, float ury, int rotation) : this(0, 0, urx, ury, rotation) {}

        public PdfRectangle(float urx, float ury) : this(0, 0, urx, ury, 0) {}
    
        /**
         * Constructs a <CODE>PdfRectangle</CODE>-object with a <CODE>Rectangle</CODE>-object.
         *
         * @param   rectangle   a <CODE>Rectangle</CODE>
         */
    
        public PdfRectangle(Rectangle rectangle, int rotation) : this(rectangle.Left, rectangle.Bottom, rectangle.Right, rectangle.Top, rotation) {}
    
        public PdfRectangle(Rectangle rectangle) : this(rectangle.Left, rectangle.Bottom, rectangle.Right, rectangle.Top, 0) {}
    
        // methods
    
        /**
        * Returns the high level version of this PdfRectangle
        * @return this PdfRectangle translated to class Rectangle
        */
        public Rectangle Rectangle {
            get {
                return new Rectangle(Left, Bottom, Right, Top);
            }
        }

        /**
         * Overrides the <CODE>add</CODE>-method in <CODE>PdfArray</CODE> in order to prevent the adding of extra object to the array.
         *
         * @param       object          <CODE>PdfObject</CODE> to add (will not be added here)
         * @return      <CODE>false</CODE>
         */
    
        public override bool Add(PdfObject obj) {
            return false;
        }
    
        /**
         * Returns the lower left x-coordinate.
         *
         * @return      the lower left x-coordinaat
         */
    
        public float Left {
            get {
                return llx;
            }
        }
    
        /**
         * Returns the upper right x-coordinate.
         *
         * @return      the upper right x-coordinate
         */
    
        public float Right {
            get {
                return urx;
            }
        }
    
        /**
         * Returns the upper right y-coordinate.
         *
         * @return      the upper right y-coordinate
         */
    
        public float Top {
            get {
                return ury;
            }
        }
    
        /**
         * Returns the lower left y-coordinate.
         *
         * @return      the lower left y-coordinate
         */
    
        public float Bottom {
            get {
                return lly;
            }
        }
    
        /**
         * Returns the lower left x-coordinate, considering a given margin.
         *
         * @param       margin      a margin
         * @return      the lower left x-coordinate
         */
    
        public float GetLeft(int margin) {
            return llx + margin;
        }
    
        /**
         * Returns the upper right x-coordinate, considering a given margin.
         *
         * @param       margin      a margin
         * @return      the upper right x-coordinate
         */
    
        public float GetRight(int margin) {
            return urx - margin;
        }
    
        /**
         * Returns the upper right y-coordinate, considering a given margin.
         *
         * @param       margin      a margin
         * @return      the upper right y-coordinate
         */
    
        public float GetTop(int margin) {
            return ury - margin;
        }
    
        /**
         * Returns the lower left y-coordinate, considering a given margin.
         *
         * @param       margin      a margin
         * @return      the lower left y-coordinate
         */
    
        public float GetBottom(int margin) {
            return lly + margin;
        }
    
        /**
         * Returns the width of the rectangle.
         *
         * @return      a width
         */
    
        public float Width {
            get {
                return urx - llx;
            }
        }
    
        /**
         * Returns the height of the rectangle.
         *
         * @return      a height
         */
    
        public float Height {
            get {
                return ury - lly;
            }
        }
    
        /**
         * Swaps the values of urx and ury and of lly and llx in order to rotate the rectangle.
         *
         * @return      a <CODE>PdfRectangle</CODE>
         */
    
        public PdfRectangle Rotate {
            get {
                return new PdfRectangle(lly, llx, ury, urx, 0);
            }
        }
    }
}