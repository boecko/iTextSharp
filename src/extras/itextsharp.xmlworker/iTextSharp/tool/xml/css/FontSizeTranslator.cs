using System;
using System.Globalization;
using System.util;
using iTextSharp.tool.xml;
/**
 *
 */
namespace iTextSharp.tool.xml.css {

    /**
     * @author Emiel Ackermann
     *
     */
    public class FontSizeTranslator {

        /**
         *
         */
        private static CssUtils utils = CssUtils.GetInstance();
        private static FontSizeTranslator myself;
        private static object syncroot = new object();

        /**
         * @return Singleton instance of FontSizeTranslater.
         */
        public static FontSizeTranslator GetInstance() {
                if (myself != null)
                    return myself;
                lock (syncroot) {
                    if (null == myself) {
                        myself = new FontSizeTranslator();
                    }
                    return myself;
                }
        }

        /**
         * Returns the css value of the style <b>font-size</b> in a pt-value. Possible font-size values:
         * <ul>
         *  <li>a constant in px, in, cm, mm, pc, em or ex,</li>
         *  <li>xx-small,</li>
         *  <li>x-small,</li>
         *  <li>small,</li>
         *  <li>medium,</li>
         *  <li>large,</li>
         *  <li>x-large,</li>
         *  <li>xx-large,</li>
         *  <li>smaller (than tag's parent size),</li>
         *  <li>larger (than tag's parent size),</li>
         *  <li>a percentage (e.g font-size:250%) of tag's parent size,</li>
         * </ul>
         * @param tag to get the font size of.
         * @return float font size of the content of the tag in pt.
         */
        public float TranslateFontSize(Tag tag) {
            float size = 12;
            if (tag.CSS.ContainsKey(CSS.Property.FONT_SIZE)) {
                String value = tag.CSS[CSS.Property.FONT_SIZE];
                 if (Util.EqualsIgnoreCase(value, CSS.Value.XX_SMALL)){
                     size = 6.75f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.X_SMALL)){
                     size = 7.5f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.SMALL)){
                     size = 9.75f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.MEDIUM)){
                     size = 12f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.LARGE)){
                     size = 13.5f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.X_LARGE)){
                     size = 18f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.XX_LARGE)){
                     size = 24f;
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.SMALLER)){
                     if (tag.Parent != null) {
                         float parentSize = GetFontSize(tag.Parent); // if the font-size of the parent can be set in some memory the translation part is not needed anymore.
                         if (parentSize <= 6.75f){
                             size = parentSize-1;
                         } else if (parentSize == 7.5f){
                             size = 6.75f;
                         } else if (parentSize == 9.75f){
                             size = 7.5f;
                         } else if (parentSize == 12f){
                             size = 9.75f;
                         } else if (parentSize == 13.5f){
                             size = 12f;
                         } else if (parentSize == 18f){
                             size = 13.5f;
                         } else if (parentSize == 24f){
                             size = 18f;
                         } else if (parentSize < 24f){
                             size = parentSize * 0.85f;
                         } else if (parentSize >= 24) {
                             size = parentSize * 2 / 3;
                         }
                     } else {
                         size = 9.75f;
                     }
                 } else if (Util.EqualsIgnoreCase(value, CSS.Value.LARGER)){
                     if (tag.Parent != null) {
                         float parentSize = GetFontSize(tag.Parent); // if the font-size of the parent can be set in some memory the translation part is not needed anymore.
                         if (parentSize == 6.75f){
                             size = 7.5f;
                         } else if (parentSize == 7.5f){
                             size = 9.75f;
                         } else if (parentSize == 9.75f){
                             size = 12f;
                         } else if (parentSize == 12f){
                             size = 13.5f;
                         } else if (parentSize == 13.5f){
                             size = 18f;
                         } else if (parentSize == 18f){
                             size = 24f;
                         } else {
                             size = parentSize * 1.5f;
                         }
                     } else {
                         size = 13.5f;
                     }
                 } else if (utils.IsMetricValue(value)||utils.IsNumericValue(value)){
                     size = utils.ParsePxInCmMmPcToPt(value);
                 } else if (utils.IsRelativeValue(value)) {
                    float baseValue = 0;
                    if (tag.Parent != null) {
                        baseValue = GetFontSize(tag.Parent);
                    } else {
                        baseValue = 12;
                    }
                    size = utils.ParseRelativeValue(value, baseValue);
                 }
            }
            return size;
        }

        /**
         * Retrieves the pt font size from {@link Tag#getCSS()} with {@link CSS.Property#FONT_SIZE} or returns default 12pt
         * @param tag the tag to get the font-size from.
         * @return the font size
         */
        public float GetFontSize(Tag tag) {
            String str;
            tag.CSS.TryGetValue(CSS.Property.FONT_SIZE, out str);
            if (null != str) {
                return float.Parse(str.Replace("pt", ""), CultureInfo.InvariantCulture);
            }
            return 12 ;
        }
    }
}