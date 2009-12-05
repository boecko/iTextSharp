using System;
using System.Collections;
using System.IO;
using iTextSharp.text.error_messages;

/*
 * $Id: PdfContentParser.cs,v 1.4 2006/09/17 15:55:03 psoares33 Exp $
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

namespace iTextSharp.text.pdf {
    /**
    * Parses the page or template content.
    * @author Paulo Soares
    */
    public class PdfContentParser {
        
        /**
        * Commands have this type.
        */    
        public const int COMMAND_TYPE = 200;
        /**
        * Holds value of property tokeniser.
        */
        private PRTokeniser tokeniser;    
        
        /**
        * Creates a new instance of PdfContentParser
        * @param tokeniser the tokeniser with the content
        */
        public PdfContentParser(PRTokeniser tokeniser) {
            this.tokeniser = tokeniser;
        }
        
        /**
        * Parses a single command from the content. Each command is output as an array of arguments
        * having the command itself as the last element. The returned array will be empty if the
        * end of content was reached.
        * @param ls an <CODE>ArrayList</CODE> to use. It will be cleared before using. If it's
        * <CODE>null</CODE> will create a new <CODE>ArrayList</CODE>
        * @return the same <CODE>ArrayList</CODE> given as argument or a new one
        * @throws IOException on error
        */    
        public ArrayList Parse(ArrayList ls) {
            if (ls == null)
                ls = new ArrayList();
            else
                ls.Clear();
            PdfObject ob = null;
            while ((ob = ReadPRObject()) != null) {
                ls.Add(ob);
                if (ob.Type == COMMAND_TYPE)
                    break;
            }
            return ls;
        }
        
        /**
        * Gets the tokeniser.
        * @return the tokeniser.
        */
        public PRTokeniser GetTokeniser() {
            return this.tokeniser;
        }
        
        /**
        * Sets the tokeniser.
        * @param tokeniser the tokeniser
        */
        public PRTokeniser Tokeniser {
            set {
                tokeniser = value;
            }
            get {
                return tokeniser;
            }
        }
        
        /**
        * Reads a dictionary. The tokeniser must be positioned past the "&lt;&lt;" token.
        * @return the dictionary
        * @throws IOException on error
        */    
        public PdfDictionary ReadDictionary() {
            PdfDictionary dic = new PdfDictionary();
            while (true) {
                if (!NextValidToken())
                    throw new IOException(MessageLocalization.GetComposedMessage("unexpected.end.of.file"));
                    if (tokeniser.TokenType == PRTokeniser.TK_END_DIC)
                        break;
                    if (tokeniser.TokenType != PRTokeniser.TK_NAME)
                        throw new IOException(MessageLocalization.GetComposedMessage("dictionary.key.is.not.a.name"));
                    PdfName name = new PdfName(tokeniser.StringValue, false);
                    PdfObject obj = ReadPRObject();
                    int type = obj.Type;
                    if (-type == PRTokeniser.TK_END_DIC)
                        throw new IOException(MessageLocalization.GetComposedMessage("unexpected.gt.gt"));
                    if (-type == PRTokeniser.TK_END_ARRAY)
                        throw new IOException(MessageLocalization.GetComposedMessage("unexpected.close.bracket"));
                    dic.Put(name, obj);
            }
            return dic;
        }
        
        /**
        * Reads an array. The tokeniser must be positioned past the "[" token.
        * @return an array
        * @throws IOException on error
        */    
        public PdfArray ReadArray() {
            PdfArray array = new PdfArray();
            while (true) {
                PdfObject obj = ReadPRObject();
                int type = obj.Type;
                if (-type == PRTokeniser.TK_END_ARRAY)
                    break;
                if (-type == PRTokeniser.TK_END_DIC)
                    throw new IOException(MessageLocalization.GetComposedMessage("unexpected.gt.gt"));
                array.Add(obj);
            }
            return array;
        }
        
        /**
        * Reads a pdf object.
        * @return the pdf object
        * @throws IOException on error
        */    
        public PdfObject ReadPRObject() {
            if (!NextValidToken())
                return null;
            int type = tokeniser.TokenType;
            switch (type) {
                case PRTokeniser.TK_START_DIC: {
                    PdfDictionary dic = ReadDictionary();
                    return dic;
                }
                case PRTokeniser.TK_START_ARRAY:
                    return ReadArray();
                case PRTokeniser.TK_STRING:
                    PdfString str = new PdfString(tokeniser.StringValue, null).SetHexWriting(tokeniser.IsHexString());
                    return str;
                case PRTokeniser.TK_NAME:
                    return new PdfName(tokeniser.StringValue, false);
                case PRTokeniser.TK_NUMBER:
                    return new PdfNumber(tokeniser.StringValue);
                 case PRTokeniser.TK_OTHER:
                    return new PdfLiteral(COMMAND_TYPE, tokeniser.StringValue);
                default:
                    return new PdfLiteral(-type, tokeniser.StringValue);
            }
        }
        
        /**
        * Reads the next token skipping over the comments.
        * @return <CODE>true</CODE> if a token was read, <CODE>false</CODE> if the end of content was reached
        * @throws IOException on error
        */    
        public bool NextValidToken() {
            while (tokeniser.NextToken()) {
                if (tokeniser.TokenType == PRTokeniser.TK_COMMENT)
                    continue;
                return true;
            }
            return false;
        }
    }
}
