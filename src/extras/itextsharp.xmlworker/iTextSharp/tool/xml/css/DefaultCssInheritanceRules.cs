using System;
using System.Collections.Generic;
using System.util;
using iTextSharp.tool.xml;
using iTextSharp.tool.xml.html;
/*
 * $Id: DefaultCssInheritanceRules.java 123 2011-05-27 12:30:40Z redlab_b $
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
namespace iTextSharp.tool.xml.css {

    /**
     * @author redlab_b
     *
     */
    public class DefaultCssInheritanceRules : ICssInheritanceRules {

        /*
         * (non-Javadoc)
         *
         * @see
         * com.itextpdf.tool.xml.css.CssInheritanceRules#inheritCssTag(java.lang
         * .String)
         */
        public bool InheritCssTag(String tag) {
            return true;
        }

        private static readonly IList<String> GLOBAL = new List<string>(new String[] { "width", "height", "min-width", "max-width", "min-height",
                "max-height", "margin", "margin-left", "margin-right", "margin-top",
                "margin-bottom", "padding", "padding-left", "padding-right", "padding-top", "padding-bottom",
                "border-top-width", "border-top-style", "border-top-color", "border-bottom-width",
                "border-bottom-style", "border-bottom-color", "border-left-width", "border-left-style",
                "border-left-color", "border-right-width", "border-right-style", "border-right-color",
                CSS.Property.PAGE_BREAK_BEFORE ,CSS.Property.PAGE_BREAK_AFTER });
        private static readonly IList<String> PARENT_TO_TABLE = new List<string>(new String[] { "line-height", "font-size", "font-style", "font-weight",
                "text-indent" });
        private static readonly IList<String> TABLE_IN_ROW = new List<string>(new String[] { "background-color" });
        // styles that should not be applied on the content of a td-tag.
        private static readonly IList<String> TD_TO_CONTENT = new List<string>(new String[] { "vertical-align" });

        /*
         * (non-Javadoc)
         *
         * @see
         * com.itextpdf.tool.xml.css.CssInheritanceRules#inheritCssSelector(com.
         * itextpdf.tool.xml.Tag, java.lang.String)
         */
        public bool InheritCssSelector(Tag tag, String key) {
            if (GLOBAL.Contains(key)) {
                return false;
            }
            if (HTML.Tag.TABLE.Equals(tag.TagName)) {
                return !PARENT_TO_TABLE.Contains(key);
            }
            if (HTML.Tag.TABLE.Equals(tag.Parent.TagName)) {
                return !TABLE_IN_ROW.Contains(key);
            }
            if (Util.EqualsIgnoreCase(HTML.Tag.TD, tag.Parent.TagName)) {
                return !TD_TO_CONTENT.Contains(key);
            }
            return true;
        }
    }
}