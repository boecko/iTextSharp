using System;
using System.IO;
using System.Text;
using iTextSharp.text.exceptions;
using iTextSharp.text.error_messages;

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

namespace iTextSharp.text.pdf {

    /**
     *
     * @author  Paulo Soares
     */
    public class PRTokeniser {
    
        public enum TokType {
            NUMBER = 1,
            STRING,
            NAME,
            COMMENT,
            START_ARRAY,
            END_ARRAY,
            START_DIC,
            END_DIC,
            REF,
            OTHER,
            ENDOFFILE
        }
    
        internal const string EMPTY = "";

    
        protected RandomAccessFileOrArray file;
        protected TokType type;
        protected string stringValue;
        protected int reference;
        protected int generation;
        protected bool hexString;
        
        public PRTokeniser(string filename) {
            file = new RandomAccessFileOrArray(filename);
        }

        public PRTokeniser(byte[] pdfIn) {
            file = new RandomAccessFileOrArray(pdfIn);
        }
    
        public PRTokeniser(RandomAccessFileOrArray file) {
            this.file = file;
        }

        public void Seek(int pos) {
            file.Seek(pos);
        }
    
        public int FilePointer {
            get {
                return file.FilePointer;
            }
        }

        public void Close() {
            file.Close();
        }
    
        public int Length {
            get {
                return file.Length;
            }
        }

        public int Read() {
            return file.Read();
        }
    
        public RandomAccessFileOrArray SafeFile {
            get {
                return new RandomAccessFileOrArray(file);
            }
        }
    
        public RandomAccessFileOrArray File {
            get {
                return file;
            }
        }

        public string ReadString(int size) {
            StringBuilder buf = new StringBuilder();
            int ch;
            while ((size--) > 0) {
                ch = file.Read();
                if (ch == -1)
                    break;
                buf.Append((char)ch);
            }
            return buf.ToString();
        }

        public static bool IsWhitespace(int ch) {
            return (ch == 0 || ch == 9 || ch == 10 || ch == 12 || ch == 13 || ch == 32);
        }
    
        public static bool IsDelimiter(int ch) {
            return (ch == '(' || ch == ')' || ch == '<' || ch == '>' || ch == '[' || ch == ']' || ch == '/' || ch == '%');
        }

        public TokType TokenType {
            get {
                return type;
            }
        }
    
        public string StringValue {
            get {
                return stringValue;
            }
        }
    
        public int Reference {
            get {
                return reference;
            }
        }
    
        public int Generation {
            get {
                return generation;
            }
        }
    
        public void BackOnePosition(int ch) {
            if (ch != -1)
                file.PushBack((byte)ch);
        }
    
        public void ThrowError(string error) {
            throw new InvalidPdfException(MessageLocalization.GetComposedMessage("1.at.file.pointer.2", error, file.FilePointer));
        }
    
        public char CheckPdfHeader() {
            file.StartOffset = 0;
            String str = ReadString(1024);
            int idx = str.IndexOf("%PDF-");
            if (idx < 0)
                throw new InvalidPdfException(MessageLocalization.GetComposedMessage("pdf.header.not.found"));
            file.StartOffset = idx;
            return str[idx + 7];
        }
        
        public void CheckFdfHeader() {
            file.StartOffset = 0;
            String str = ReadString(1024);
            int idx = str.IndexOf("%FDF-1.2");
            if (idx < 0)
                throw new InvalidPdfException(MessageLocalization.GetComposedMessage("fdf.header.not.found"));
            file.StartOffset = idx;
        }

        public int Startxref {
            get {
                int size = Math.Min(1024, file.Length);
                int pos = file.Length - size;
                file.Seek(pos);
                string str = ReadString(1024);
                int idx = str.LastIndexOf("startxref");
                if (idx < 0)
                    throw new InvalidPdfException(MessageLocalization.GetComposedMessage("pdf.startxref.not.found"));
                return pos + idx;
            }
        }

        public static int GetHex(int v) {
            if (v >= '0' && v <= '9')
                return v - '0';
            if (v >= 'A' && v <= 'F')
                return v - 'A' + 10;
            if (v >= 'a' && v <= 'f')
                return v - 'a' + 10;
            return -1;
        }
    
        public void NextValidToken() {
            int level = 0;
            string n1 = null;
            string n2 = null;
            int ptr = 0;
            while (NextToken()) {
                if (type == TokType.COMMENT)
                    continue;
                switch (level) {
                    case 0: {
                        if (type != TokType.NUMBER)
                            return;
                        ptr = file.FilePointer;
                        n1 = stringValue;
                        ++level;
                        break;
                    }
                    case 1: {
                        if (type != TokType.NUMBER) {
                            file.Seek(ptr);
                            type = TokType.NUMBER;
                            stringValue = n1;
                            return;
                        }
                        n2 = stringValue;
                        ++level;
                        break;
                    }
                    default: {
                        if (type != TokType.OTHER || !stringValue.Equals("R")) {
                            file.Seek(ptr);
                            type = TokType.NUMBER;
                            stringValue = n1;
                            return;
                        }
                        type = TokType.REF;
                        reference = int.Parse(n1);
                        generation = int.Parse(n2);
                        return;
                    }
                }
            }
            // if we hit here, the file is either corrupt (stream ended unexpectedly),
            // or the last token ended exactly at the end of a stream.  This last
            // case can occur inside an Object Stream.
        }
    
        public bool NextToken() {
            int ch = 0;
            do {
                ch = file.Read();
            } while (ch != -1 && IsWhitespace(ch));
            if (ch == -1){
                type = TokType.ENDOFFILE;
                return false;
            }
            // Note:  We have to initialize stringValue here, after we've looked for the end of the stream,
            // to ensure that we don't lose the value of a token that might end exactly at the end
            // of the stream
            StringBuilder outBuf = null;
            stringValue = EMPTY;
            switch (ch) {
                case '[':
                    type = TokType.START_ARRAY;
                    break;
                case ']':
                    type = TokType.END_ARRAY;
                    break;
                case '/': {
                    outBuf = new StringBuilder();
                    type = TokType.NAME;
                    while (true) {
                        ch = file.Read();
                        if (ch == -1 || IsDelimiter(ch) || IsWhitespace(ch))
                            break;
                        if (ch == '#') {
                            ch = (GetHex(file.Read()) << 4) + GetHex(file.Read());
                        }
                        outBuf.Append((char)ch);
                    }
                    BackOnePosition(ch);
                    break;
                }
                case '>':
                    ch = file.Read();
                    if (ch != '>')
                        ThrowError(MessageLocalization.GetComposedMessage("greaterthan.not.expected"));
                    type = TokType.END_DIC;
                    break;
                case '<': {
                    int v1 = file.Read();
                    if (v1 == '<') {
                        type = TokType.START_DIC;
                        break;
                    }
                    outBuf = new StringBuilder();
                    type = TokType.STRING;
                    hexString = true;
                    int v2 = 0;
                    while (true) {
                        while (IsWhitespace(v1))
                            v1 = file.Read();
                        if (v1 == '>')
                            break;
                        v1 = GetHex(v1);
                        if (v1 < 0)
                            break;
                        v2 = file.Read();
                        while (IsWhitespace(v2))
                            v2 = file.Read();
                        if (v2 == '>') {
                            ch = v1 << 4;
                            outBuf.Append((char)ch);
                            break;
                        }
                        v2 = GetHex(v2);
                        if (v2 < 0)
                            break;
                        ch = (v1 << 4) + v2;
                        outBuf.Append((char)ch);
                        v1 = file.Read();
                    }
                    if (v1 < 0 || v2 < 0)
                        ThrowError(MessageLocalization.GetComposedMessage("error.reading.string"));
                    break;
                }
                case '%':
                    type = TokType.COMMENT;
                    do {
                        ch = file.Read();
                    } while (ch != -1 && ch != '\r' && ch != '\n');
                    break;
                case '(': {
                    outBuf = new StringBuilder();
                    type = TokType.STRING;
                    hexString = false;
                    int nesting = 0;
                    while (true) {
                        ch = file.Read();
                        if (ch == -1)
                            break;
                        if (ch == '(') {
                            ++nesting;
                        }
                        else if (ch == ')') {
                            --nesting;
                        }
                        else if (ch == '\\') {
                            bool lineBreak = false;
                            ch = file.Read();
                            switch (ch) {
                                case 'n':
                                    ch = '\n';
                                    break;
                                case 'r':
                                    ch = '\r';
                                    break;
                                case 't':
                                    ch = '\t';
                                    break;
                                case 'b':
                                    ch = '\b';
                                    break;
                                case 'f':
                                    ch = '\f';
                                    break;
                                case '(':
                                case ')':
                                case '\\':
                                    break;
                                case '\r':
                                    lineBreak = true;
                                    ch = file.Read();
                                    if (ch != '\n')
                                        BackOnePosition(ch);
                                    break;
                                case '\n':
                                    lineBreak = true;
                                    break;
                                default: {
                                    if (ch < '0' || ch > '7') {
                                        break;
                                    }
                                    int octal = ch - '0';
                                    ch = file.Read();
                                    if (ch < '0' || ch > '7') {
                                        BackOnePosition(ch);
                                        ch = octal;
                                        break;
                                    }
                                    octal = (octal << 3) + ch - '0';
                                    ch = file.Read();
                                    if (ch < '0' || ch > '7') {
                                        BackOnePosition(ch);
                                        ch = octal;
                                        break;
                                    }
                                    octal = (octal << 3) + ch - '0';
                                    ch = octal & 0xff;
                                    break;
                                }
                            }
                            if (lineBreak)
                                continue;
                            if (ch < 0)
                                break;
                        }
                        else if (ch == '\r') {
                            ch = file.Read();
                            if (ch < 0)
                                break;
                            if (ch != '\n') {
                                BackOnePosition(ch);
                                ch = '\n';
                            }
                        }
                        if (nesting == -1)
                            break;
                        outBuf.Append((char)ch);
                    }
                    if (ch == -1)
                        ThrowError(MessageLocalization.GetComposedMessage("error.reading.string"));
                    break;
                }
                default: {
                    outBuf = new StringBuilder();
                    if (ch == '-' || ch == '+' || ch == '.' || (ch >= '0' && ch <= '9')) {
                        type = TokType.NUMBER;
                        do {
                            outBuf.Append((char)ch);
                            ch = file.Read();
                        } while (ch != -1 && ((ch >= '0' && ch <= '9') || ch == '.'));
                    }
                    else {
                        type = TokType.OTHER;
                        do {
                            outBuf.Append((char)ch);
                            ch = file.Read();
                        } while (ch != -1 && !IsDelimiter(ch) && !IsWhitespace(ch));
                    }
                    BackOnePosition(ch);
                    break;
                }
            }
            if (outBuf != null)
                stringValue = outBuf.ToString();
            return true;
        }
    
        public int IntValue {
            get {
                return int.Parse(stringValue);
            }
        }

        public bool ReadLineSegment(byte[] input) {
            int c = -1;
            bool eol = false;
            int ptr = 0;
            int len = input.Length;
            // ssteward, pdftk-1.10, 040922: 
            // skip initial whitespace; added this because PdfReader.RebuildXref()
            // assumes that line provided by readLineSegment does not have init. whitespace;
            if ( ptr < len ) {
                while ( IsWhitespace( (c = Read()) ) );
            }
            while ( !eol && ptr < len ) {
                switch (c) {
                    case -1:
                    case '\n':
                        eol = true;
                        break;
                    case '\r':
                        eol = true;
                        int cur = FilePointer;
                        if ((Read()) != '\n') {
                            Seek(cur);
                        }
                        break;
                    default:
                        input[ptr++] = (byte)c;
                        break;
                }

                // break loop? do it before we Read() again
                if ( eol || len <= ptr ) {
                    break;
                }
                else {
                    c = Read();
                }
            }
            if (ptr >= len) {
                eol = false;
                while (!eol) {
                    switch (c = Read()) {
                        case -1:
                        case '\n':
                            eol = true;
                            break;
                        case '\r':
                            eol = true;
                            int cur = FilePointer;
                            if ((Read()) != '\n') {
                                Seek(cur);
                            }
                            break;
                    }
                }
            }
            
            if ((c == -1) && (ptr == 0)) {
                return false;
            }
            if (ptr + 2 <= len) {
                input[ptr++] = (byte)' ';
                input[ptr] = (byte)'X';
            }
            return true;
        }
        
        public static int[] CheckObjectStart(byte[] line) {
            try {
                PRTokeniser tk = new PRTokeniser(line);
                int num = 0;
                int gen = 0;
                if (!tk.NextToken() || tk.TokenType != TokType.NUMBER)
                    return null;
                num = tk.IntValue;
                if (!tk.NextToken() || tk.TokenType != TokType.NUMBER)
                    return null;
                gen = tk.IntValue;
                if (!tk.NextToken())
                    return null;
                if (!tk.StringValue.Equals("obj"))
                    return null;
                return new int[]{num, gen};
            }
            catch {
            }
            return null;
        }
        
        public bool IsHexString() {
            return this.hexString;
        }
        
    }
}
