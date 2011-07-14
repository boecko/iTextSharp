using System;
using System.Collections.Generic;
using iTextSharp.tool.xml;
using System.util;
/*
 * $Id: CssFilesImpl.java 123 2011-05-27 12:30:40Z redlab_b $
 *
 * This file is part of the iText (R) project. Copyright (c) 1998-2011 1T3XT BVBA Authors: Balder Van Camp, Emiel
 * Ackermann, et al.
 *
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU Affero General
 * Public License version 3 as published by the Free Software Foundation with the addition of the following permission
 * added to Section 15 as permitted in Section 7(a): FOR ANY PART OF THE COVERED WORK IN WHICH THE COPYRIGHT IS OWNED BY
 * 1T3XT, 1T3XT DISCLAIMS THE WARRANTY OF NON INFRINGEMENT OF THIRD PARTY RIGHTS.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
 * warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more
 * details. You should have received a copy of the GNU Affero General Public License along with this program; if not,
 * see http://www.gnu.org/licenses or write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
 * Boston, MA, 02110-1301 USA, or download the license from the following URL: http://itextpdf.com/terms-of-use/
 *
 * The interactive user interfaces in modified source and object code versions of this program must display Appropriate
 * Legal Notices, as required under Section 5 of the GNU Affero General Public License.
 *
 * In accordance with Section 7(b) of the GNU Affero General Public License, a covered work must retain the producer
 * line in every PDF that is created or manipulated using iText.
 *
 * You can be released from the requirements of the license by purchasing a commercial license. Buying such a license is
 * mandatory as soon as you develop commercial activities involving the iText software without disclosing the source
 * code of your own applications. These activities include: offering paid services to customers as an ASP, serving PDFs
 * on the fly in a web application, shipping iText with a closed source product.
 *
 * For more information, please contact iText Software Corp. at this address: sales@itextpdf.com
 */
namespace iTextSharp.tool.xml.css {

    /**
     * @author itextpdf.com
     *
     */
    public class CssFilesImpl : ICssFiles {

        private IList<ICssFile> files;
        private CssUtils utils;
        private CssSelector select;

        /**
         * Constructs a new CssFilesImpl.
         */
        public CssFilesImpl() {
            this.files = new List<ICssFile>();
            this.utils = CssUtils.GetInstance();
            this.select = new CssSelector();
        }

        /**
         * Construct a new CssFilesImpl with the given css file.
         * @param css the css file
         */
        public CssFilesImpl(ICssFile css) : this() {
            this.Add(css);
        }

        /*
         * (non-Javadoc)
         *
         * @see com.itextpdf.tool.xml.css.CssFiles#hasFiles()
         */
        public bool HasFiles() {
            return this.files.Count != 0;
        }

        /**
         * Processes a tag and retrieves CSS. Selectors created:
         * <ul>
         * <li>element</li>
         * <li>element&gt;element (and a spaced version element &gt; element)</li>
         * <li>#id</li>
         * <li>.class</li>
         * <li>element+element ( and a spaced version element + element)</li>
         * </ul>
         */
        public IDictionary<String, String> GetCSS(Tag t) {
            IDictionary<String, String> aggregatedProps = new Dictionary<String, String>();
            IDictionary<String,object> childSelectors = select.CreateAllSelectors(t);
            foreach (String selector in childSelectors.Keys) {
                PopulateCss(aggregatedProps, selector);
            }
            return aggregatedProps;
        }

        /**
         * @param aggregatedProps the map to put the properties in.
         * @param selector the selector to search for.
         */
        public void PopulateCss(IDictionary<String, String> aggregatedProps, String selector) {
            foreach (ICssFile cssFile in this.files) {
                IDictionary<String, String> t = cssFile.Get(selector);
                IDictionary<String, String> css = new Dictionary<String, String>();
                foreach (KeyValuePair<String, String> e in t) {
                    String key = utils.StripDoubleSpacesAndTrim(e.Key);
                    String value = utils.StripDoubleSpacesAndTrim(e.Value);
                    if (Util.EqualsIgnoreCase(CSS.Property.BORDER, key)) {
                        CssUtils.MapPutAll(css, utils.ParseBorder(value));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.MARGIN, key)) {
                        CssUtils.MapPutAll(css, utils.ParseBoxValues(value, "margin-", ""));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.BORDER_WIDTH, key)) {
                        CssUtils.MapPutAll(css, utils.ParseBoxValues(value, "border-", "-width"));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.BORDER_STYLE, key)) {
                        CssUtils.MapPutAll(css, utils.ParseBoxValues(value, "border-", "-style"));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.BORDER_COLOR, key)) {
                        CssUtils.MapPutAll(css, utils.ParseBoxValues(value, "border-", "-color"));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.PADDING, key)) {
                        CssUtils.MapPutAll(css, utils.ParseBoxValues(value, "padding-", ""));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.FONT, key)) {
                        CssUtils.MapPutAll(css, utils.ProcessFont(value));
                    } else if (Util.EqualsIgnoreCase(CSS.Property.LIST_STYLE, key)) {
                        CssUtils.MapPutAll(css, utils.ProcessListStyle(value));
                    } else {
                        css[key] = value;
                    }
                }
                CssUtils.MapPutAll(aggregatedProps, css);
            }
        }

        /* (non-Javadoc)
         * @see com.itextpdf.tool.xml.css.CssFiles#addFile(com.itextpdf.tool.xml.css.CssFile)
         */
        public void Add(ICssFile css) {
            this.files.Add(css);
        }

        /* (non-Javadoc)
         * @see com.itextpdf.tool.xml.css.CssFiles#clear()
         */
        public void Clear() {
            for (int k = 0; k < files.Count; ++k) {
                if (!files[k].IsPersistent()) {
                    files.RemoveAt(k);
                    --k;
                }
            }
        }
    }
}