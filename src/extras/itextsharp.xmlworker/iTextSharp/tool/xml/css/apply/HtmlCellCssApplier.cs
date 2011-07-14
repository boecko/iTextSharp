using System;
using System.Collections.Generic;
using System.Globalization;
using System.util;
using iTextSharp.text;
using iTextSharp.text.html;
using iTextSharp.tool.xml;
using iTextSharp.tool.xml.css;
using iTextSharp.tool.xml.html;
using iTextSharp.tool.xml.html.pdfelement;
using iTextSharp.tool.xml.html.table;
using iTextSharp.tool.xml.pipeline.html;
/*
 * $Id: HtmlCellCssApplier.java 170 2011-06-08 10:06:43Z emielackermann $
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
namespace iTextSharp.tool.xml.css.apply {

    /**
     * @author Emiel Ackermann
     *
     */
    public class HtmlCellCssApplier : ICssApplier<HtmlCell> {

        private CssUtils utils = CssUtils.GetInstance();
        private HtmlPipelineContext configuration;


        /**
         * @param htmlPipelineContext the context
         */
        public HtmlCellCssApplier(HtmlPipelineContext htmlPipelineContext) {
            this.configuration = htmlPipelineContext;
        }
        /*
         * (non-Javadoc)
         *
         * @see
         * com.itextpdf.tool.xml.css.CssApplier#apply(com.itextpdf.text.Element,
         * com.itextpdf.tool.xml.Tag)
         */
        public HtmlCell Apply(HtmlCell cell, Tag t) {
            TableStyleValues values = new TableStyleValues();
            Tag table = t.Parent;
            while (!table.TagName.Equals("table")){
                table = table.Parent;
            }
            String border;
            table.Attributes.TryGetValue(CSS.Property.BORDER, out border);
            if (border != null && !border.Equals("0")) {
                values.BorderColor = BaseColor.BLACK;
                values.BorderWidth = 0.75f;
            }
            IDictionary<String, String> css = t.CSS;
            String emptyCells;
            css.TryGetValue(CSS.Property.EMPTY_CELLS, out emptyCells);
            if (null != emptyCells && Util.EqualsIgnoreCase(CSS.Value.HIDE, emptyCells) && cell.CompositeElements == null) {
                cell.Border = Rectangle.NO_BORDER;
            } else {
                cell.VerticalAlignment = Element.ALIGN_MIDDLE; // Default css behavior. Implementation of "vertical-align" style further along.
                if (t.Attributes.ContainsKey(HTML.Attribute.WIDTH) || css.ContainsKey(HTML.Attribute.WIDTH)) {
                    cell.FixedWidth = new WidthCalculator().GetWidth(t, configuration.GetRootTags(), configuration.GetPageSize().Width);
                }
                String colspan;
                t.Attributes.TryGetValue(HTML.Attribute.COLSPAN, out colspan);
                if (null != colspan) {
                    cell.Colspan = int.Parse(colspan);
                }
                String rowspan;
                t.Attributes.TryGetValue(HTML.Attribute.ROWSPAN, out rowspan);
                if (null != rowspan) {
                    cell.Rowspan = int.Parse(rowspan);
                }
                foreach (KeyValuePair<String, String> entry in css) {
                    String key = entry.Key;
                    String value = entry.Value;
                    cell.UseBorderPadding = true;
                    if (Util.EqualsIgnoreCase(key, CSS.Property.HEIGHT)) {
                        cell.MinimumHeight = utils.ParsePxInCmMmPcToPt(value);
                    } else if (Util.EqualsIgnoreCase(key, CSS.Property.BACKGROUND_COLOR)) {
                        values.Background = HtmlUtilities.DecodeColor(value);
                    } else if (Util.EqualsIgnoreCase(key, CSS.Property.VERTICAL_ALIGN)) {
                        if (Util.EqualsIgnoreCase(value, CSS.Value.TOP)) {
                            cell.VerticalAlignment = Element.ALIGN_TOP;
                            cell.PaddingTop = cell.PaddingTop+6;
                        } else if (Util.EqualsIgnoreCase(value, CSS.Value.BOTTOM)) {
                            cell.VerticalAlignment = Element.ALIGN_BOTTOM;
                            cell.PaddingBottom = cell.PaddingBottom+6;
                        }
                    } else if (key.Contains(CSS.Property.BORDER)) {
                        if (key.Contains(CSS.Value.TOP)) {
                            SetTopOfBorder(cell, key, value, values);
                        } else if (key.Contains(CSS.Value.BOTTOM)) {
                            SetBottomOfBorder(cell, key, value, values);
                        } else if (key.Contains(CSS.Value.LEFT)) {
                            SetLeftOfBorder(cell, key, value, values);
                        } else if (key.Contains(CSS.Value.RIGHT)) {
                            SetRightOfBorder(cell, key, value, values);
                        }
                    } else if (key.Contains(CSS.Property.CELLPADDING) || key.Contains(CSS.Property.PADDING)) {
                        if (key.Contains(CSS.Value.TOP)) {
                            cell.PaddingTop = cell.PaddingTop+utils.ParsePxInCmMmPcToPt(value);
                        } else if (key.Contains(CSS.Value.BOTTOM)) {
                            cell.PaddingBottom = cell.PaddingBottom+utils.ParsePxInCmMmPcToPt(value);
                        } else if (key.Contains(CSS.Value.LEFT)) {
                            cell.PaddingLeft = cell.PaddingLeft+utils.ParsePxInCmMmPcToPt(value);
                        } else if (key.Contains(CSS.Value.RIGHT)) {
                            cell.PaddingRight = cell.PaddingRight+utils.ParsePxInCmMmPcToPt(value);
                        }
                    } else if (key.Contains(CSS.Property.TEXT_ALIGN)) {
                        if (Util.EqualsIgnoreCase(value, CSS.Value.LEFT)) {
                            cell.HorizontalAlignment = Element.ALIGN_LEFT;
                        } else if (Util.EqualsIgnoreCase(value, CSS.Value.CENTER)) {
                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        } else if (Util.EqualsIgnoreCase(value, CSS.Value.RIGHT)) {
                            cell.HorizontalAlignment = Element.ALIGN_RIGHT;
                        }
                    }
                }
                float horSpacing = new Table().GetBorderOrCellSpacing(true, table.CSS, table.Attributes);
                float verSpacing = new Table().GetBorderOrCellSpacing(false, table.CSS, table.Attributes);
                values.HorBorderSpacing = horSpacing;
                values.VerBorderSpacing = verSpacing;
                cell.PaddingLeft = cell.PaddingLeft+horSpacing+values.BorderWidthLeft;
                cell.PaddingRight = cell.PaddingRight+values.BorderWidthRight;
                cell.PaddingTop = cell.PaddingTop+verSpacing+values.BorderWidthTop;
                cell.PaddingBottom = cell.PaddingBottom+values.BorderWidthBottom+1;
            }
            cell.Border = Rectangle.NO_BORDER;
            cell.CellEvent = new CellSpacingEvent(values);
            cell.CellValues = values;
            return cell;
        }

        private void SetTopOfBorder(HtmlCell cell, String key, String value, TableStyleValues values) {
            if (key.Contains(CSS.Property.WIDTH)) {
                values.BorderWidthTop = utils.ParsePxInCmMmPcToPt(value);
            }
            if (key.Contains(CSS.Property.COLOR)) {
                values.BorderColorTop = HtmlUtilities.DecodeColor(value);
            } else if (values.BorderColorTop == null){
                values.BorderColorTop = BaseColor.BLACK;
            }
            if (key.Contains("style")) {
    //          If any, which are the border styles in iText? simulate in the borderevent?
                if (values.BorderWidthTop == 0){
                    values.BorderWidthTop = 2.25f;
                }
            }
        }
        private void SetBottomOfBorder(HtmlCell cell, String key, String value, TableStyleValues values) {
            if (key.Contains(CSS.Property.WIDTH)) {
                values.BorderWidthBottom = utils.ParsePxInCmMmPcToPt(value);
            }
            if (key.Contains(CSS.Property.COLOR)) {
                values.BorderColorBottom = HtmlUtilities.DecodeColor(value);
            } else if (values.BorderColorBottom == null){
                values.BorderColorBottom = BaseColor.BLACK;
            }
            if (key.Contains("style")) {
    //          If any, which are the border styles in iText? simulate in the borderevent?
                if (values.BorderWidthBottom == 0){
                    values.BorderWidthBottom = 2.25f;
                }
            }
        }
        private void SetLeftOfBorder(HtmlCell cell, String key, String value, TableStyleValues values) {
            if (key.Contains(CSS.Property.WIDTH)) {
                values.BorderWidthLeft = utils.ParsePxInCmMmPcToPt(value);
            }
            if (key.Contains(CSS.Property.COLOR)) {
                values.BorderColorLeft = HtmlUtilities.DecodeColor(value);
            } else if (values.BorderColorLeft == null){
                values.BorderColorLeft = BaseColor.BLACK;
            }
            if (key.Contains("style")) {
    //          If any, which are the border styles in iText? simulate in the borderevent?
                if (values.BorderWidthLeft == 0){
                    values.BorderWidthLeft = 2.25f;
                }
            }
        }
        private void SetRightOfBorder(HtmlCell cell, String key, String value, TableStyleValues values) {
            if (key.Contains(CSS.Property.WIDTH)) {
                values.BorderWidthRight = utils.ParsePxInCmMmPcToPt(value);
            }
            if (key.Contains(CSS.Property.COLOR)) {
                values.BorderColorRight = HtmlUtilities.DecodeColor(value);
            } else if (values.BorderColorRight == null){
                values.BorderColorRight = BaseColor.BLACK;
            }
            if (key.Contains("style")) {
    //          If any, which are the border styles in iText? simulate in the borderevent?
                if (values.BorderWidthRight == 0){
                    values.BorderWidthRight = 2.25f;
                }
            }
        }
    }
}