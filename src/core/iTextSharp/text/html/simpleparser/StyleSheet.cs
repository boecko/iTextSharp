using System;
using System.Collections.Generic;
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

namespace iTextSharp.text.html.simpleparser {

    public class StyleSheet {
        
        public Dictionary<String, Dictionary<String, String>> classMap = new Dictionary<String, Dictionary<String, String>>();
        public Dictionary<String, Dictionary<String, String>> tagMap = new Dictionary<String, Dictionary<String, String>>();
        
        /** Creates a new instance of StyleSheet */
        public StyleSheet() {
        }
        
        public void ApplyStyle(String tag, Dictionary<String, String> props) {
            Dictionary<String, String> map;
            Dictionary<String, String> temp;
            if (tagMap.TryGetValue(tag.ToLower(System.Globalization.CultureInfo.InvariantCulture), out map)) {
                temp = new Dictionary<String, String>(map);
                foreach (KeyValuePair<string,string> dc in props)
                    temp[dc.Key] = dc.Value;
                foreach (KeyValuePair<string,string> dc in temp)
                    props[dc.Key] = dc.Value;
            }
            String cm;
            if (!props.TryGetValue(Markup.HTML_ATTR_CSS_CLASS, out cm))
                return;
            if (!classMap.TryGetValue(cm.ToLower(System.Globalization.CultureInfo.InvariantCulture), out map))
                return;
            props.Remove(Markup.HTML_ATTR_CSS_CLASS);
            foreach (KeyValuePair<string,string> dc in props)
                map[dc.Key] = dc.Value;
            foreach (KeyValuePair<string,string> dc in map)
                props[dc.Key] = dc.Value;
        }
        
        public void LoadStyle(String style, Dictionary<String, String> props) {
            classMap[style.ToLower(System.Globalization.CultureInfo.InvariantCulture)] = props;
        }

        public void LoadStyle(String style, String key, String value) {
            style = style.ToLower(System.Globalization.CultureInfo.InvariantCulture);
            Dictionary<String, String> props;
            if (!classMap.TryGetValue(style, out props)) {
                props = new Dictionary<string,string>();
                classMap[style] = props;
            }
            props[key] = value;
        }
        
        public void LoadTagStyle(String tag, Dictionary<string,string> props) {
            tagMap[tag.ToLower(System.Globalization.CultureInfo.InvariantCulture)] = props;
        }

        public void LoadTagStyle(String tag, String key, String value) {
            tag = tag.ToLower(System.Globalization.CultureInfo.InvariantCulture);
            Dictionary<String, String> props;
            if (!tagMap.TryGetValue(tag, out props)) {
                props = new Dictionary<string,string>();
                tagMap[tag] = props;
            }
            props[key] = value;
        }
    }
}
