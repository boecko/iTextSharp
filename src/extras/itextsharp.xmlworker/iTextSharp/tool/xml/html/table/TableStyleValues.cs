using System;
using iTextSharp.text;
/*
 * $Id: TableStyleValues.java 122 2011-05-27 12:20:58Z redlab_b $
 *
 * This file is part of the iText (R) project.
 * Copyright (c) 1998-2011 1T3XT BVBA
 * Authors: Balder Van Camp, Emiel Ackermann, et al.
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
 * a covered work must retain the producer line in every PDF that is created
 * or manipulated using iText.
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
namespace iTextSharp.tool.xml.html.table {

    /**
     * Class is used for storing style values of a table or cell. The stored style values are used when drawing the borders and background colors of tables or cells.
     *
     * @author Emiel Ackermann
     *
     */
    public class TableStyleValues {
        private float horBorderSpacing;

        public float HorBorderSpacing {
            get { return horBorderSpacing; }
            set { horBorderSpacing = value; }
        }
        private float verBorderSpacing;

        public float VerBorderSpacing {
            get { return verBorderSpacing; }
            set { verBorderSpacing = value; }
        }
        private float borderLeftWidth;

        public float BorderWidthLeft {
            get { return borderLeftWidth; }
            set { borderLeftWidth = value; }
        }
        private BaseColor borderLeftColor;

        public BaseColor BorderColorLeft {
            get { return borderLeftColor; }
            set { borderLeftColor = value; }
        }
        private float borderRightWidth;

        public float BorderWidthRight {
            get { return borderRightWidth; }
            set { borderRightWidth = value; }
        }
        private BaseColor borderRightColor;

        public BaseColor BorderColorRight {
            get { return borderRightColor; }
            set { borderRightColor = value; }
        }
        private float borderTopWidth;

        public float BorderWidthTop {
            get { return borderTopWidth; }
            set { borderTopWidth = value; }
        }
        private BaseColor borderTopColor;

        public BaseColor BorderColorTop {
            get { return borderTopColor; }
            set { borderTopColor = value; }
        }
        private float borderBottomWidth;

        public float BorderWidthBottom {
            get { return borderBottomWidth; }
            set { borderBottomWidth = value; }
        }
        private BaseColor borderBottomColor;

        public BaseColor BorderColorBottom {
            get { return borderBottomColor; }
            set { borderBottomColor = value; }
        }
        private bool isLastInRow = false;

        public bool IsLastInRow {
            get { return isLastInRow; }
            set { isLastInRow = value; }
        }
        private BaseColor background;

        public BaseColor Background {
            get { return background; }
            set { background = value; }
        }

        /**
         * Default constructor.
         */
        public TableStyleValues() {
        }
        /**
         * Sets the border width for the entire table or cell.
         * @param width float.
         */
        public float BorderWidth {
            set {
                borderBottomWidth = value;
                borderLeftWidth = value;
                borderRightWidth = value;
                borderTopWidth = value;
            }
        }
        /**
         * Sets the border color for the entire table or cell.
         * @param color BaseColor.
         */
        public BaseColor BorderColor {
            set {
                borderBottomColor = value;
                borderLeftColor = value;
                borderRightColor = value;
                borderTopColor = value;
            }
        }
    }
}