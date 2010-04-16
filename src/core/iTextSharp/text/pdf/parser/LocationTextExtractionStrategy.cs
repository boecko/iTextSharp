using System;
using System.Collections.Generic;
using System.Text;
/*
 * $Id: SimpleTextExtractingPdfContentRenderListener.java 4115 2009-12-01 14:08:23Z blowagie $
 *
 * This file is part of the iText project.
 * Copyright (c) 1998-2009 1T3XT BVBA
 * Authors: Kevin Day, Bruno Lowagie, Paulo Soares, et al.
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
namespace iTextSharp.text.pdf.parser {


    /**
     * <b>Development preview</b> - this class (and all of the parser classes) are still experiencing
     * heavy development, and are subject to change both behavior and interface.
     * <br>
     * A text extraction renderer that keeps track of relative position of text on page
     * The resultant text will be relatively consistent with the physical layout that most
     * PDF files have on screen.
     * <br>
     * This renderer keeps track of the orientation and distance (both perpendicular
     * and parallel) to the unit vector of the orientation.  Text is ordered by
     * orientation, then perpendicular, then parallel distance.  Text with the same
     * perpendicular distance, but different parallel distance is treated as being on
     * the same line.
     * <br>
     * This renderer also uses a simple strategy based on the font metrics to determine if
     * a blank space should be inserted into the output.
     *
     * @since   5.0.2
     */
    public class LocationTextExtractionStrategy : ITextExtractionStrategy {

        /** set to true for debugging */
        public static bool DUMP_STATE = false;
        
        /** a summary of all found text */
        private List<TextChunk> locationalResult = new List<TextChunk>();

        /**
         * Creates a new text extraction renderer.
         */
        public LocationTextExtractionStrategy() {
        }

        /**
         * @see com.itextpdf.text.pdf.parser.RenderListener#beginTextBlock()
         */
        public void BeginTextBlock(){
        }

        /**
         * @see com.itextpdf.text.pdf.parser.RenderListener#endTextBlock()
         */
        public void EndTextBlock(){
        }

        /**
         * Returns the result so far.
         * @return  a String with the resulting text.
         */
        public String GetResultantText(){

            if (DUMP_STATE) DumpState();
            
            locationalResult.Sort();

            StringBuilder sb = new StringBuilder();
            TextChunk lastChunk = null;
            foreach (TextChunk chunk in locationalResult) {

                if (lastChunk == null){
                    sb.Append(chunk.text);
                } else {
                    if (chunk.SameLine(lastChunk)){
                        float dist = chunk.DistanceFromEndOf(lastChunk);
                        
                        if (dist < -chunk.charSpaceWidth)
                            sb.Append(' ');

                        // we only insert a blank space if the trailing character of the previous string wasn't a space, and the leading character of the current string isn't a space
                        else if (dist > chunk.charSpaceWidth/2.0f && chunk.text[0] != ' ' && lastChunk.text[lastChunk.text.Length-1] != ' ')
                            sb.Append(' ');

                        sb.Append(chunk.text);
                    } else {
                        sb.Append('\n');
                        sb.Append(chunk.text);
                    }
                }
                lastChunk = chunk;
            }

            return sb.ToString();

        }

        /** Used for debugging only */
        private void DumpState(){
            foreach (TextChunk location in locationalResult) {
                
                location.PrintDiagnostics();
                
                Console.WriteLine();
            }
            
        }
        
        /**
         * 
         * @see com.itextpdf.text.pdf.parser.RenderListener#renderText(com.itextpdf.text.pdf.parser.TextRenderInfo)
         */
        public void RenderText(TextRenderInfo renderInfo) {
            LineSegment segment = renderInfo.GetBaseline();
            TextChunk location = new TextChunk(renderInfo.GetText(), segment.GetStartPoint(), segment.GetEndPoint(), renderInfo.GetSingleSpaceWidth());
            locationalResult.Add(location);        
        }
        


        /**
         * Represents a chunk of text, it's orientation, and location relative to the orientation vector
         */
        private class TextChunk : IComparable<TextChunk>{
            /** the text of the chunk */
            internal String text;
            /** the starting location of the chunk */
            internal Vector startLocation;
            /** the ending location of the chunk */
            internal Vector endLocation;
            /** unit vector in the orientation of the chunk */
            internal Vector orientationVector;
            /** the orientation as a scalar for quick sorting */
            internal int orientationMagnitude;
            /** perpendicular distance to the orientation unit vector (i.e. the Y position in an unrotated coordinate system)
             * we round to the nearest integer to handle the fuzziness of comparing floats */
            internal int distPerpendicular;
            /** distance of the start of the chunk parallel to the orientation unit vector (i.e. the X position in an unrotated coordinate system) */
            internal float distParallelStart;
            /** distance of the end of the chunk parallel to the orientation unit vector (i.e. the X position in an unrotated coordinate system) */
            internal float distParallelEnd;
            /** the width of a single space character in the font of the chunk */
            internal float charSpaceWidth;
            
            public TextChunk(String str, Vector startLocation, Vector endLocation, float charSpaceWidth) {
                this.text = str;
                this.startLocation = startLocation;
                this.endLocation = endLocation;
                this.charSpaceWidth = charSpaceWidth;
                
                orientationVector = endLocation.Subtract(startLocation).Normalize();
                orientationMagnitude = (int)(Math.Atan2(orientationVector[Vector.I2], orientationVector[Vector.I1])*1000);

                // see http://mathworld.wolfram.com/Point-LineDistance2-Dimensional.html
                // the two vectors we are crossing are in the same plane, so the result will be purely
                // in the z-axis (out of plane) direction, so we just take the I3 component of the result
                Vector origin = new Vector(0,0,1);
                distPerpendicular = (int)(startLocation.Subtract(origin)).Cross(orientationVector)[Vector.I3];

                distParallelStart = orientationVector.Dot(startLocation);
                distParallelEnd = orientationVector.Dot(endLocation);
            }

            public void PrintDiagnostics(){
                Console.WriteLine("Text (@" + startLocation + " -> " + endLocation + "): " + text);
                Console.WriteLine("orientationMagnitude: " + orientationMagnitude);
                Console.WriteLine("distPerpendicular: " + distPerpendicular);
                Console.WriteLine("distParallel: " + distParallelStart);
            }
            
            /**
             * @param as the location to compare to
             * @return true is this location is on the the same line as the other
             */
            public bool SameLine(TextChunk a){
                if (orientationMagnitude != a.orientationMagnitude) return false;
                if (distPerpendicular != a.distPerpendicular) return false;
                return true;
            }

            /**
             * Computes the distance between the end of 'other' and the beginning of this chunk
             * in the direction of this chunk's orientation vector.  Note that it's a bad idea
             * to call this for chunks that aren't on the same line and orientation, but we don't
             * explicitly check for that condition for performance reasons.
             * @param other
             * @return the number of spaces between the end of 'other' and the beginning of this chunk
             */
            public float DistanceFromEndOf(TextChunk other){
                float distance = distParallelStart - other.distParallelEnd;
                return distance;
            }
            
            /**
             * Compares based on orientation, perpendicular distance, then parallel distance
             * @see java.lang.Comparable#compareTo(java.lang.Object)
             */
            public int CompareTo(TextChunk rhs) {
                if (this == rhs) return 0; // not really needed, but just in case
                
                int rslt;
                rslt = CompareInts(orientationMagnitude, rhs.orientationMagnitude);
                if (rslt != 0) return rslt;

                rslt = CompareInts(distPerpendicular, rhs.distPerpendicular);
                if (rslt != 0) return rslt;

                // note: it's never safe to check floating point numbers for equality, and if two chunks
                // are truly right on top of each other, which one comes first or second just doesn't matter
                // so we arbitrarily choose this way.
                rslt = distParallelStart < rhs.distParallelStart ? -1 : 1;

                return rslt;
            }

            /**
             *
             * @param int1
             * @param int2
             * @return comparison of the two integers
             */
            private static int CompareInts(int int1, int int2){
                return int1 == int2 ? 0 : int1 < int2 ? -1 : 1;
            }

            
        }

        /**
         * no-op method - this renderer isn't interested in image events
         * @see com.itextpdf.text.pdf.parser.RenderListener#renderImage(com.itextpdf.text.pdf.parser.ImageRenderInfo)
         * @since 5.0.1
         */
        public void RenderImage(ImageRenderInfo renderInfo) {
            // do nothing
        }
    }
}