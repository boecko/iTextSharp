using System;
using System.Collections.Generic;
using System.util;
using iTextSharp.text;
using iTextSharp.text.log;
using iTextSharp.text.pdf;
using iTextSharp.text.html;
using iTextSharp.tool.xml;
using iTextSharp.tool.xml.css;
using iTextSharp.tool.xml.css.apply;
using iTextSharp.tool.xml.exceptions;
using iTextSharp.tool.xml.html;
using iTextSharp.tool.xml.html.pdfelement;
using iTextSharp.tool.xml.html.table;
using iTextSharp.tool.xml.pipeline.html;
/*
 * $Id: Table.java 170 2011-06-08 10:06:43Z emielackermann $
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
     * @author Emiel Ackermann
     *
     */
    public class Table : AbstractTagProcessor {

        private static ILogger LOG = LoggerFactory.GetLogger(typeof(Table));
        private static CssUtils utils = CssUtils.GetInstance();
        private static FontSizeTranslator fst = FontSizeTranslator.GetInstance();

        /**
         * Default constructor.
         */
        public Table() {
        }

        /*
         * (non-Javadoc)
         *
         * @see
         * com.itextpdf.tool.xml.ITagProcessor#endElement(com.itextpdf.tool.xml.Tag,
         * java.util.List, com.itextpdf.text.Document)
         */
        public override IList<IElement> End(IWorkerContext ctx, Tag tag, IList<IElement> currentContent) {
            try {
                int numberOfColumns = 0;
                List<TableRowElement> tableRows = new List<TableRowElement>(currentContent.Count);
                IList<IElement> invalidRowElements = new List<IElement>(1);
                String repeatHeader;
                tag.CSS.TryGetValue(CSS.Property.REPEAT_HEADER, out repeatHeader);
                String repeatFooter;
                tag.CSS.TryGetValue(CSS.Property.REPEAT_FOOTER, out repeatFooter);
                int headerRows = 0;
                int footerRows = 0;
                foreach (IElement e in currentContent) {
                    int localNumCols = 0;
                    if (e is TableRowElement) {
                        TableRowElement tableRowElement = (TableRowElement) e;
                        foreach (HtmlCell cell in tableRowElement.Content) {
                            localNumCols += cell.Colspan;
                        }
                        if (localNumCols > numberOfColumns) {
                            numberOfColumns = localNumCols;
                        }
                        tableRows.Add(tableRowElement);
                        if (repeatHeader != null && Util.EqualsIgnoreCase(repeatHeader, "yes") && tableRowElement.RowPlace.Equals(TableRowElement.Place.HEADER)) {
                            headerRows++;
                        }
                        if (repeatFooter != null && Util.EqualsIgnoreCase(repeatFooter, "yes") && tableRowElement.RowPlace.Equals(TableRowElement.Place.FOOTER)){
                            footerRows++;
                        }
                    } else {
                        invalidRowElements.Add(e);
                    }
                }
                if(repeatFooter == null || !Util.EqualsIgnoreCase(repeatFooter, "yes")) {
                    tableRows.Sort(delegate(TableRowElement o1, TableRowElement o2) {
                        return o1.RowPlace.Normal.CompareTo(o2.RowPlace.Normal);
                    });
                } else {
                    tableRows.Sort(delegate(TableRowElement o1, TableRowElement o2) {
                        return o1.RowPlace.Repeated.CompareTo(o2.RowPlace.Repeated);
                    });
                }
                PdfPTable table = new PdfPTable(numberOfColumns);
                table.HeaderRows = headerRows+footerRows;
                table.FooterRows = footerRows;
                TableStyleValues styleValues = SetStyleValues(tag);
                table.TableEvent = new TableBorderEvent(styleValues);
                SetVerticalMargin(table, tag, styleValues, ctx);
                WidenLastCell(tableRows, styleValues.HorBorderSpacing);
                float[] columnWidths = new float[numberOfColumns];
                float[] widestWords = new float[numberOfColumns];
                float[] fixedWidths = new float[numberOfColumns];
                int[] rowspanValue = new int[numberOfColumns];
                float largestColumn = 0;
                int indexOfLargestColumn = 0;
                // Initial fill of the widths arrays
                foreach (TableRowElement row in tableRows) {
                    int column = 0;
                    foreach (HtmlCell cell in row.Content) {
                        // check whether the current column should be skipped due to a
                        // rowspan value of higher cell in this column.
                        while (rowspanValue[column] > 1) {
                            rowspanValue[column] = rowspanValue[column] - 1;
                            ++column;
                        }
                        // sets a rowspan counter for current column (counter not
                        // needed for last column).
                        if (cell.Rowspan > 1 && column != numberOfColumns - 1) {
                            rowspanValue[column] = cell.Rowspan - 1;
                        }
                        int colspan = cell.Colspan;
                        if (cell.FixedWidth != 0) {
                            float fixedWidth = cell.FixedWidth + GetCellStartWidth(cell);
                            fixedWidth /= colspan;
                            for (int i = 0; i < colspan; i++) {
                                int c = column + i;
                                if (fixedWidth > fixedWidths[c]) {
                                    fixedWidths[c] = fixedWidth;
                                    columnWidths[c] = fixedWidth;
                                }
                            }
                        }
                        if (cell.CompositeElements != null) {
                            float[] widthValues = SetCellWidthAndWidestWord(cell);
                            float cellWidth = widthValues[0] / colspan;
                            float widestWordOfCell = widthValues[1] / colspan;
                            for (int i = 0; i < colspan; i++) {
                                int c = column + i;
                                if (fixedWidths[c] == 0 && cellWidth > columnWidths[c]) {
                                    columnWidths[c] = cellWidth;
                                    if (cellWidth > largestColumn) {
                                        largestColumn = cellWidth;
                                        indexOfLargestColumn = c;
                                    }
                                }
                                if (widestWordOfCell > widestWords[c]) {
                                    widestWords[c] = widestWordOfCell;
                                }
                            }
                        }
                        if (colspan > 1) {
                            if (LOG.IsLogging(Level.TRACE)) {
                                LOG.Trace(String.Format(LocaleMessages.GetInstance().GetMessage(LocaleMessages.COLSPAN), colspan));
                            }
                            column += colspan - 1;
                        }
                        column++;
                    }
                }
                float outerWidth = GetTableOuterWidth(tag, styleValues.HorBorderSpacing, ctx);
                float initialTotalWidth = GetTableWidth(columnWidths, 0);
    //          float targetWidth = calculateTargetWidth(tag, columnWidths, outerWidth, ctx);
                float targetWidth = 0;
                HtmlPipelineContext htmlPipelineContext = GetHtmlPipelineContext(ctx);
                float max = htmlPipelineContext.GetPageSize().Width - outerWidth;
                bool tableWidthFixed = false;
                if (tag.Attributes.ContainsKey(CSS.Property.WIDTH) || tag.CSS.ContainsKey(CSS.Property.WIDTH)) {
                    targetWidth = new WidthCalculator().GetWidth(tag, htmlPipelineContext.GetRootTags(), htmlPipelineContext.GetPageSize().Width);
                    if (targetWidth > max) {
                        targetWidth = max;
                    }
                    tableWidthFixed = true;
                } else if (initialTotalWidth <= max) {
                    targetWidth = initialTotalWidth;
                } else if (null == tag.Parent || (null != tag.Parent && htmlPipelineContext.GetRootTags().Contains(tag.Parent.TagName))) {
                    targetWidth = max;
                } else /* this table is an inner table and width adjustment is done in outer table */{
                    targetWidth = GetTableWidth(columnWidths, outerWidth);
                }
                float totalFixedColumnWidth = GetTableWidth(fixedWidths, 0);
                float targetPercentage = 0;
                if (totalFixedColumnWidth == initialTotalWidth) { // all column widths are fixed
                    targetPercentage = targetWidth / initialTotalWidth;
                    if (initialTotalWidth > targetWidth) {
                        for (int column = 0; column < columnWidths.Length; column++) {
                            columnWidths[column] *= targetPercentage;
                        }
                    } else if(tableWidthFixed && targetPercentage != 1){
                        for (int column = 0; column < columnWidths.Length; column++) {
                            columnWidths[column] *= targetPercentage;
                        }
                    }
                } else {
                    targetPercentage = (targetWidth - totalFixedColumnWidth) / (initialTotalWidth - totalFixedColumnWidth);
                    // Reduce width of columns if the columnWidth array + borders +
                    // paddings
                    // is too large for the given targetWidth.
                    if (initialTotalWidth > targetWidth) {
                        float leftToReduce = 0;
                        for (int column = 0; column < columnWidths.Length; column++) {
                            if (fixedWidths[column] == 0) {
                                // Reduce width of the column to its targetWidth, if
                                // widestWord of column still fits in the targetWidth of
                                // the
                                // column.
                                if (widestWords[column] <= columnWidths[column] * targetPercentage) {
                                    columnWidths[column] *= targetPercentage;
                                    // else take the widest word and calculate space
                                    // left to
                                    // reduce.
                                } else {
                                    columnWidths[column] = widestWords[column];
                                    leftToReduce += widestWords[column] - columnWidths[column] * targetPercentage;
                                }
                                // if widestWord of a column does not fit in the
                                // fixedWidth,
                                // set the column width to the widestWord.
                            } else if (fixedWidths[column] < widestWords[column]) {
                                columnWidths[column] = widestWords[column];
                                leftToReduce += widestWords[column] - fixedWidths[column];
                            }
                        }
                        if (leftToReduce != 0) {
                            // Reduce width of the column with the most text, if its
                            // widestWord still fits in the reduced column.
                            if (widestWords[indexOfLargestColumn] <= columnWidths[indexOfLargestColumn] - leftToReduce) {
                                columnWidths[indexOfLargestColumn] -= leftToReduce;
                            } else { // set all columns to their minimum, with the
                                        // widestWord array.
                                for (int column = 0; leftToReduce != 0 && column < columnWidths.Length; column++) {
                                    if (fixedWidths[column] == 0 && columnWidths[column] > widestWords[column]) {
                                        float difference = columnWidths[column] - widestWords[column];
                                        if (difference <= leftToReduce) {
                                            leftToReduce -= difference;
                                            columnWidths[column] = widestWords[column];
                                        } else {
                                            columnWidths[column] -= leftToReduce;
                                            leftToReduce = 0;
                                        }
                                    }
                                }
                                if (leftToReduce != 0) {
                                    // If the table has an insufficient fixed width
                                    // by
                                    // an
                                    // attribute or style, try to enlarge the table
                                    // to
                                    // its
                                    // minimum width (= widestWords array).
                                    float pageWidth = GetHtmlPipelineContext(ctx).GetPageSize().Width;
                                    if (GetTableWidth(widestWords, outerWidth) < pageWidth) {
                                        targetWidth = GetTableWidth(widestWords, outerWidth);
                                        leftToReduce = 0;
                                    } else {
                                        // If all columnWidths are set to the
                                        // widestWordWidths and the table is still
                                        // to
                                        // wide
                                        // content will fall off the edge of a page,
                                        // which
                                        // is similar to HTML.
                                        targetWidth = pageWidth - outerWidth;
                                        leftToReduce = 0;
                                    }
                                }
                            }
                        }
                        // Enlarge width of columns to fit the targetWidth.
                    } else if (initialTotalWidth < targetWidth) {
                        for (int column = 0; column < columnWidths.Length; column++) {
                            if (fixedWidths[column] == 0) {
                                columnWidths[column] *= targetPercentage;
                            }
                        }
                    }
                }
                try {
                    table.SetTotalWidth(columnWidths);
                    table.LockedWidth = true;
                    table.DefaultCell.Border = Rectangle.NO_BORDER;
                } catch (DocumentException e) {
                    throw new RuntimeWorkerException(LocaleMessages.GetInstance().GetMessage(LocaleMessages.NO_CUSTOM_CONTEXT), e);
                }
                foreach (TableRowElement row in tableRows) {
                    int columnNumber = -1;
                    foreach (HtmlCell cell in row.Content) {
                        columnNumber += cell.Colspan;
                        IList<IElement> compositeElements = cell.CompositeElements;
                        if (compositeElements != null) {
                            foreach (IElement baseLevel in compositeElements) {
                                if (baseLevel is PdfPTable) {
                                    TableStyleValues cellValues = cell.CellValues;
                                    float totalBordersWidth = cellValues.IsLastInRow ? styleValues.HorBorderSpacing * 2
                                            : styleValues.HorBorderSpacing;
                                    totalBordersWidth += cellValues.BorderWidthLeft + cellValues.BorderWidthRight;
                                    float columnWidth = columnWidths[columnNumber];
                                    IPdfPTableEvent tableEvent = ((PdfPTable) baseLevel).TableEvent;
                                    TableStyleValues innerStyleValues = ((TableBorderEvent) tableEvent).TableStyleValues;
                                    totalBordersWidth += innerStyleValues.BorderWidthLeft;
                                    totalBordersWidth += innerStyleValues.BorderWidthRight;
                                    ((PdfPTable) baseLevel).TotalWidth = columnWidth - totalBordersWidth;
                                }
                            }
                        }
                        table.AddCell(cell);
                    }
                    table.CompleteRow();
                }
                List<IElement> elems = new List<IElement>();
                if (invalidRowElements.Count > 0) {
                    // all invalid row elements taken as caption
                    int i = 0;
                    Tag captionTag = tag.Children[i++];
                    while (!Util.EqualsIgnoreCase(captionTag.TagName, HTML.Tag.CAPTION) && i < tag.Children.Count) {
                        captionTag = tag.Children[i];
                        i++;
                    }
                    String captionSideValue;
                    captionTag.CSS.TryGetValue(CSS.Property.CAPTION_SIDE, out captionSideValue);
                    if (captionSideValue != null && Util.EqualsIgnoreCase(captionSideValue, CSS.Value.BOTTOM)) {
                        elems.Add(table);
                        elems.AddRange(invalidRowElements);
                    } else {
                        elems.AddRange(invalidRowElements);
                        elems.Add(table);
                    }
                } else {
                    elems.Add(table);
                }
                return elems;
            } catch (NoCustomContextException e) {
                throw new RuntimeWorkerException(LocaleMessages.GetInstance().GetMessage(LocaleMessages.NO_CUSTOM_CONTEXT), e);
            }
        }

        /**
         * Calculates the target width. First checks:
         * <ol>
         * <li>if the attribute or style "width" is found in the given tag and it is not wider than pageWidth - outerWidth, then the
         * targetWidth = width value</li>
         * <li>if the columnWidths array in total is not wider than pageWidth - outerWidth, then the
         * targetWidth = the total of the columnWidths array</li>
         * <li>if table's parent is a root tag or table has no parent, then the
         * targetWidth = width of the page - outerWidth
         * {@link Table#getTableOuterWidth(Tag, float)}.</li>
         * </ol>
         * If none of the above is true, the width of the table is set to its
         * default with the columnWidths array.
         *
         * @param tag containing attributes and css.
         * @param columnWidths float[] containing the widest lines of text found in
         *            the columns.
         * @param outerWidth width needed for margins and borders.
         * @param ctx
         * @return float the target width of a table.
         * @throws NoCustomContextException
         */
        private float CalculateTargetWidth(Tag tag, float[] columnWidths, float outerWidth,
                IWorkerContext ctx) {
            float targetWidth = 0;
            HtmlPipelineContext htmlPipelineContext = GetHtmlPipelineContext(ctx);
            float max = htmlPipelineContext.GetPageSize().Width - outerWidth;
            float start = GetTableWidth(columnWidths, 0);
            if (tag.Attributes.ContainsKey(CSS.Property.WIDTH) || tag.CSS.ContainsKey(CSS.Property.WIDTH)) {
                targetWidth = new WidthCalculator().GetWidth(tag, htmlPipelineContext.GetRootTags(), htmlPipelineContext
                        .GetPageSize().Width);
                if (targetWidth > max) {
                    targetWidth = max;
                }
            } else if (start <= max) {
                targetWidth = start;
            } else if (null == tag.Parent
                    || (null != tag.Parent && htmlPipelineContext.GetRootTags().Contains(tag.Parent.TagName))) {
                targetWidth = max;
            } else /*
                     * this table is an inner table and width adjustment is done in
                     * outer table
                     */{
                targetWidth = GetTableWidth(columnWidths, outerWidth);
            }
            return targetWidth;
        }

        /**
         * Adds horizontal border spacing to the right padding of the last cell of each row.
         * @param tableRows List of {@link TableRowElement} objects of the table.
         * @param horBorderSpacing float containing the horizontal border spacing of the table.
         */
        private void WidenLastCell(IList<TableRowElement> tableRows, float horBorderSpacing) {
            foreach (TableRowElement row in tableRows) {
                IList<HtmlCell> cells = row.Content;
                HtmlCell last = cells[cells.Count - 1];
                last.CellValues.IsLastInRow = true;
                last.PaddingRight = last.PaddingRight + horBorderSpacing;
            }
        }

        /** Set the table style values in a {@link TableStyleValues} object based on attributes and css of the given tag.
         * @param tag containing attributes and css.
         * @return a {@link TableStyleValues} object containing the table's style values.
         */
        private TableStyleValues SetStyleValues(Tag tag) {
            TableStyleValues styleValues = new TableStyleValues();
            IDictionary<String, String> css = tag.CSS;
            IDictionary<String, String> attributes = tag.Attributes;
            string v;
            if (attributes.ContainsKey(CSS.Property.BORDER)) {
                styleValues.BorderColor = BaseColor.BLACK;
                styleValues.BorderWidth = utils.ParsePxInCmMmPcToPt(attributes[CSS.Property.BORDER]);
            } else {
                css.TryGetValue(CSS.Property.BORDER_BOTTOM_COLOR, out v);
                styleValues.BorderColorBottom = HtmlUtilities.DecodeColor(v);
                css.TryGetValue(CSS.Property.BORDER_TOP_COLOR, out v);
                styleValues.BorderColorTop = HtmlUtilities.DecodeColor(v);
                css.TryGetValue(CSS.Property.BORDER_LEFT_COLOR, out v);
                styleValues.BorderColorLeft = HtmlUtilities.DecodeColor(v);
                css.TryGetValue(CSS.Property.BORDER_RIGHT_COLOR, out v);
                styleValues.BorderColorRight = HtmlUtilities.DecodeColor(v);
                styleValues.BorderWidthBottom = utils.CheckMetricStyle(css, CSS.Property.BORDER_BOTTOM_WIDTH);
                styleValues.BorderWidthTop = utils.CheckMetricStyle(css, CSS.Property.BORDER_TOP_WIDTH);
                styleValues.BorderWidthLeft = utils.CheckMetricStyle(css, CSS.Property.BORDER_LEFT_WIDTH);
                styleValues.BorderWidthRight = utils.CheckMetricStyle(css, CSS.Property.BORDER_RIGHT_WIDTH);
            }
            css.TryGetValue(CSS.Property.BACKGROUND_COLOR, out v);
            styleValues.Background = HtmlUtilities.DecodeColor(v);
            styleValues.HorBorderSpacing = GetBorderOrCellSpacing(true, css, attributes);
            styleValues.VerBorderSpacing = GetBorderOrCellSpacing(false, css, attributes);
            return styleValues;
        }

        /**
         * Extracts and parses the style border-spacing or the attribute cellspacing
         * of a table tag, if present. Favors the style border-spacing over the
         * attribute cellspacing. <br />
         * If style="border-collapse:collapse" is found in the css, the spacing is
         * always 0f. <br />
         * If no spacing is set, the default of 1.5pt is returned.
         *
         * @param getHor true for horizontal spacing, false for vertical spacing.
         * @param css of the table tag.
         * @param attributes of the table tag.
         * @return horizontal or vertical spacing between two cells or a cell and
         *         the border of the table.
         */
        public float GetBorderOrCellSpacing(bool getHor, IDictionary<String, String> css, IDictionary<String, String> attributes) {
            float spacing = 1.5f;
            String collapse;
            css.TryGetValue("border-collapse", out collapse);
            if(collapse == null || collapse.Equals("seperate")) {
                String borderSpacing;
                css.TryGetValue("border-spacing", out borderSpacing);
                String cellSpacing;
                attributes.TryGetValue("cellspacing", out cellSpacing);
                String borderAttr;
                attributes.TryGetValue("border", out borderAttr);
                if(borderSpacing != null) {
                    if(borderSpacing.Contains(" ")){
                        if(getHor) {
                            spacing = utils.ParsePxInCmMmPcToPt(borderSpacing.Split(' ')[0]);
                        } else {
                            spacing = utils.ParsePxInCmMmPcToPt(borderSpacing.Split(' ')[1]);
                        }
                    } else {
                        spacing = utils.ParsePxInCmMmPcToPt(borderSpacing);
                    }
                } else if (cellSpacing != null){
                    spacing = utils.ParsePxInCmMmPcToPt(cellSpacing);
                } else if (borderAttr != null){
                    spacing = 1.5f;
                }
            } else if (collapse.Equals("collapse")){
                spacing = 0;
            }
            return spacing;
        }

        /**
         * Sets the default cell width and widest word of a cell.
         * <ul>
         * <li>cell width = {@link Table#getCellStartWidth(HtmlCell)} + the width of the widest line of text.</li>
         * <li>widest word = {@link Table#getCellStartWidth(HtmlCell)} + the widest word of the cell.</li>
         * </ul>
         * These 2 widths are used as the starting point when determining the width of the table in
         * @param cell HtmlCell of which the widths are needed.
         * @return float array containing the default cell width and the widest word.
         * <ul>
         * <li>float[0] = cell width.</li>
         * <li>float[1] = widest word.</li>
         * </ul>
         */
        private float[] SetCellWidthAndWidestWord(HtmlCell cell) {
            IList<float> rulesWidth = new List<float>();
            float widestWordOfCell = 0f;
            float startWidth = GetCellStartWidth(cell);
            float cellWidth = startWidth;
            IList<IElement> compositeElements = cell.CompositeElements;
            if (compositeElements != null) {
                foreach (IElement baseLevel in compositeElements){
                    if (baseLevel is Phrase) {
                        for(int i=0; i < ((Phrase)baseLevel).Count ; i++) {
                            IElement inner = ((Phrase)baseLevel)[i];
                            if (inner is Chunk) {
                                cellWidth += ((Chunk)inner).GetWidthPoint();
                                float widestWord = startWidth + new ChunkCssApplier().GetWidestWord((Chunk) inner);
                                if(widestWord > widestWordOfCell) {
                                    widestWordOfCell = widestWord;
                                }
                            }
                        }
                        rulesWidth.Add(cellWidth);
                        cellWidth = startWidth;
                    } else if (baseLevel is List) {
                        foreach (IElement li in ((List)baseLevel).Items) {
                            rulesWidth.Add(cellWidth);
                            cellWidth = startWidth + ((ListItem)li).IndentationLeft;
                            foreach (Chunk c in((ListItem)li).Chunks) {
                                cellWidth += c.GetWidthPoint();
                                float widestWord = new ChunkCssApplier().GetWidestWord(c);
                                if(startWidth + widestWord > widestWordOfCell) {
                                    widestWordOfCell = startWidth + widestWord;
                                }
                            }
                        }
                        rulesWidth.Add(cellWidth);
                        cellWidth = startWidth;
                    } else if (baseLevel is PdfPTable) {
                        rulesWidth.Add(cellWidth);
                        cellWidth = startWidth + ((PdfPTable)baseLevel).TotalWidth;
                        foreach (PdfPRow innerRow in ((PdfPTable)baseLevel).Rows) {
                            int size = innerRow.GetCells().Length;
                            TableBorderEvent evente = (TableBorderEvent) ((PdfPTable)baseLevel).TableEvent;
                            TableStyleValues values = evente.TableStyleValues;
                            float minRowWidth = values.BorderWidthLeft+(size+1)*values.HorBorderSpacing+values.BorderWidthRight;
                            int celnr = 0;
                            foreach (PdfPCell innerCell in innerRow.GetCells()) {
                                celnr++;
                                if(innerCell != null) {
                                    float innerWidestWordOfCell = SetCellWidthAndWidestWord(new HtmlCell(innerCell, celnr == size))[1];
                                    minRowWidth += innerWidestWordOfCell;
                                }
                            }
                            if(minRowWidth > widestWordOfCell){
                                widestWordOfCell = minRowWidth;
                            }
                        }
                        rulesWidth.Add(cellWidth);
                        cellWidth = startWidth;
                    }
                }
            }
            foreach (float width in rulesWidth) {
                if(width > cellWidth) {
                    cellWidth = width;
                }
            }
        return new float[]{cellWidth, widestWordOfCell};
        }

        /**
         * Calculates the total width based on the given widths array and the given
         * outer width.
         *
         * @param widths array of floats containing column width values.
         * @param outer width equals the required space outside of the table for
         *            margins and borders.
         * @return a table's width.
         * @throws NoCustomContextException
         */
        private float GetTableWidth(float[] widths, float outerWidth) {
            float width = 0;
            foreach (float f in widths) {
                width += f;
            }
            return width + outerWidth;
        }

        /**
         * Adds horizontal values of a table and its parent if present. Following values are added up:
         * <ul>
         * <li>left and right margins of the table.</li>
         * <li>left and right border widths of the table.</li>
         * <li>left and right margins of the parent of the table is present.</li>
         * <li>one horizontal border spacing.</li>
         * </ul>
         * @param tag
         * @param horBorderSpacing
         * @return
         * @throws NoCustomContextException
         */
        private float GetTableOuterWidth(Tag tag, float horBorderSpacing, IWorkerContext ctx) {
            float total = utils.GetLeftAndRightMargin(tag, GetHtmlPipelineContext(ctx).GetPageSize().Width)
                + utils.CheckMetricStyle(tag, CSS.Property.BORDER_LEFT_WIDTH)
                + utils.CheckMetricStyle(tag, CSS.Property.BORDER_RIGHT_WIDTH)
                + horBorderSpacing;
            Tag parent = tag.Parent;
            if (parent != null) {
                total += utils.GetLeftAndRightMargin(parent, GetHtmlPipelineContext(ctx).GetPageSize().Width);
            }
            return total;
        }

        /**
         * Calculates the start width of a cell. Following values are added up:
         * <ul>
         * <li>padding left, this includes left border width and a horizontal border spacing.</li>
         * <li>padding right, this includes right border width.</li>
         * <li>the (colspan - 1) * horizontal border spacing.</li>
         * </ul>
         * @param cell HtmlCell of which the start width is needed.
         * @return float containing the start width.
         */
        private float GetCellStartWidth(HtmlCell cell) {
            TableStyleValues cellStyleValues = cell.CellValues;
            // colspan - 1, because one horBorderSpacing has been added to paddingLeft for all cells.
            int spacingMultiplier = cell.Colspan - 1;
            float spacing = spacingMultiplier*cellStyleValues.HorBorderSpacing;
            return spacing + cell.PaddingLeft+cell.PaddingRight+1;
        }

        /**
         * Sets the top and bottom margin of the given table.
         *
         * @param table PdfPTable on which the margins need to be set.
         * @param t Tag containing the margin styles and font size if needed.
         * @param values {@link TableStyleValues} containing border widths and border spacing values.
         * @throws NoCustomContextException
         */
        private void SetVerticalMargin(PdfPTable table, Tag t, TableStyleValues values, IWorkerContext ctx) {
            float spacingBefore = values.BorderWidthTop;
            IDictionary<String, Object> memory = GetHtmlPipelineContext(ctx).GetMemory();
            Object mb;
            memory.TryGetValue(HtmlPipelineContext.LAST_MARGIN_BOTTOM, out mb);
            if(mb != null) {
                spacingBefore += (float)mb;
            }
            float spacingAfter = values.VerBorderSpacing+values.BorderWidthBottom;
            foreach (KeyValuePair<String, String> css in t.CSS) {
                String key = css.Key;
                String value = css.Value;
                if(Util.EqualsIgnoreCase(CSS.Property.MARGIN_TOP, key)) {
                    spacingBefore += utils.ParseValueToPt(value, fst.GetFontSize(t));
                } else if (Util.EqualsIgnoreCase(CSS.Property.MARGIN_BOTTOM, key)) {
                    float marginBottom = utils.ParseValueToPt(value, fst.GetFontSize(t));
                    spacingAfter += marginBottom;
                    GetHtmlPipelineContext(ctx).GetMemory()[HtmlPipelineContext.LAST_MARGIN_BOTTOM] = marginBottom;
                }
            }
            table.SpacingBefore = spacingBefore;
            table.SpacingAfter = spacingAfter;
        }
        /*
         * (non-Javadoc)
         *
         * @see com.itextpdf.tool.xml.ITagProcessor#isStackOwner()
         */
        public override bool IsStackOwner() {
            return true;
        }
    }
}