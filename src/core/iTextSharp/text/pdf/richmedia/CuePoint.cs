using System;
using iTextSharp.text.pdf;
using iTextSharp.text.exceptions;
/*
 * $Id: CuePoint.java 3821 2009-03-25 10:33:52Z blowagie $
 *
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

namespace iTextSharp.text.pdf.richmedia {

    /**
     * A video file can contain cue points that are encoded in a video stream
     * or may be created by an associated ActionScript within the Flash content.
     * The CuePoint dictionary contains a state that relates the cue points to
     * an action that may be passed to the conforming application or may be used
     * to change the appearance. Cue points in the Flash content are matched to
     * the cue points declared in the PDF file by the values specified by the
     * Name or Time keys. (See ExtensionLevel 3 p91)
     * @since   5.0.0
     */
    public class CuePoint : PdfDictionary {

        /**
         * Constructs a CuePoint object.
         * A <code>Navigation</code> cue point is an event encoded in a Flash movie (FLV).
         * A chapter stop may be encoded so that when the user requests to go to or skip
         * a chapter, a navigation cue point is used to indicate the location of the chapter.
         * An <code>Event</code> is a generic cue point of no specific significance other
         * than a corresponding action is triggered.
         * @param   subtype possible values: PdfName.NAVIGATION or PdfName.EVENT
         */
        public CuePoint(PdfName subtype) : base(PdfName.CUEPOINT) {
            Put(PdfName.SUBTYPE, subtype);
        }
        
        /**
         * Set the name of the cue point to match against the cue point within
         * Flash content and for display purposes.
         * @param   name    the name of the cue point
         */
        public PdfString Name {
            set {
                Put(PdfName.NAME, value);
            }
        }
        
        /**
         * Sets the time value of the cue point in milliseconds to match against
         * the cue point within Flash content and for display purposes.
         * @param   time    the time value of the cue point
         */
        public int Time {
            set {
                Put(PdfName.TIME, new PdfNumber(value));
            }
        }

        /**
         * Sets an action dictionary defining the action that is executed
         * if this cue point is triggered, meaning that the Flash content
         * reached the matching cue point during its playback.
         * @param   action  an action
         */
        public PdfObject Action {
            set {
                if (value is PdfDictionary || value is PdfIndirectReference)
                    Put(PdfName.A, value);
                else
                    throw new IllegalPdfSyntaxException("An action should be defined as a dictionary");
            }
        }
    }
}