using System;
using System.Collections.Generic;
using System.Text;
using iTextSharp.text;
using iTextSharp.tool.xml;
using iTextSharp.tool.xml.css.apply;
using iTextSharp.tool.xml.html;
/*
 * $Id: HtmlPipelineContext.java 144 2011-06-03 22:52:42Z redlab_b $
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
namespace iTextSharp.tool.xml.pipeline.html {

    /**
     * The CustomContext object for the HtmlPipeline.<br />
     * Use this to configure your {@link HtmlPipeline}.
     * @author redlab_b
     *
     */
    public class HtmlPipelineContext : ICustomContext, ICloneable {

        /**
         *  Key for the memory, used to store bookmark nodes
         */
        public const String BOOKMARK_TREE = "header.autobookmark.RootNode";
        /**
         * Key for the memory, used in Html TagProcessing
         */
        public const String LAST_MARGIN_BOTTOM = "lastMarginBottom";
        private LinkedList<StackKeeper> queue;
        private bool acceptUnknown = true;
        private ITagProcessorFactory tagFactory;
        private IList<IElement> ctn = new List<IElement>();
        private IImageProvider imageProvider;
        private Rectangle pageSize = PageSize.A4;
        private Encoding charset;
        private IList<String> roottags = new List<string>(new String[] { "body", "div" });
        private ILinkProvider linkprovider;
        private bool autoBookmark = true;
        private IDictionary<String, Object> memory;

        /**
         * Construct a new HtmlPipelineContext object
         */
        public HtmlPipelineContext() {
            this.queue = new LinkedList<StackKeeper>();
            this.memory = new Dictionary<String, Object>();
        }
        /**
         * @param tag the tag to find a ITagProcessor for
         * @param nameSpace the namespace.
         * @return a ITagProcessor
         */
        protected internal ITagProcessor ResolveProcessor(String tag, String nameSpace) {
            ITagProcessor tp = tagFactory.GetProcessor(tag, nameSpace);
            return tp;
        }

        /**
         * Add a {@link StackKeeper} to the top of the stack list.
         * @param stackKeeper the {@link StackKeeper}
         */
        protected internal void AddFirst(StackKeeper stackKeeper) {
            this.queue.AddFirst(stackKeeper);

        }

        /**
         * Retrieves, but does not remove, the head (first element) of this list.
         * @return a StackKeeper
         * @throws NoStackException if there are no elements on the stack
         */
        protected internal StackKeeper Peek() {
            if (queue.Count == 0)
                throw new NoStackException();
            return this.queue.First.Value;
        }

        /**
         * @return the current content of elements.
         */
        protected internal IList<IElement> CurrentContent() {
            return ctn;
        }

        /**
         * @return if this pipelines tag processing accept unknown tags: true. False otherwise
         */
        public bool AcceptUnknown() {
            return this.acceptUnknown;
        }

        /**
         * @return returns true if the stack is empty
         */
        protected internal bool IsEmpty() {
            return queue.Count == 0;
        }

        /**
         * Retrieves and removes the top of the stack.
         * @return a StackKeeper
         * @throws NoStackException if there are no elements on the stack
         */
        protected internal StackKeeper Poll() {
            try {
                StackKeeper sk = this.queue.First.Value;
                this.queue.RemoveFirst();
                return sk;
            } catch (InvalidOperationException) {
                throw new NoStackException();
            }
        }
        /**
         * @return true if auto-bookmarks should be enabled. False otherwise.
         */
        public bool AutoBookmark() {
            return autoBookmark;
        }
        /**
         * @return the memory
         */
        public IDictionary<String, Object> GetMemory() {
            return memory;
        }
        /**
         * @return the image provider.
         * @throws NoImageProviderException if there is no {@link IImageProvide}
         *
         */
        public IImageProvider GetImageProvider() {
            if (null == this.imageProvider) {
                throw new NoImageProviderException();
            }
            return this.imageProvider;

        }

        /**
         * Set a {@link Charset} to use.
         * @param cSet the charset.
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext CharSet(Encoding cSet) {
            this.charset = cSet;
            return this;
        }
        /**
         * @return the {@link Charset} to use, or null if none configured.
         */
        public Encoding CharSet() {
            return charset;
        }
        /**
         * Returns a {@link Rectangle}
         * @return the pagesize.
         */
        public Rectangle GetPageSize() {
            return this.pageSize;
        }

        /**
         * @return a list of tags to be taken as root-tags. This matters for
         *         margins. By default the root-tags are &lt;body&gt; and
         *         &lt;div&gt;
         */
        public IList<String> GetRootTags() {
            return roottags;
        }

        /**
         * Returns the ILinkProvider, used to prepend e.g. http://www.example.org/ to
         * found &lt;a&gt; tags that have no absolute url.
         *
         * @return the ILinkProvider if any.
         */
        public ILinkProvider GetLinkProvider() {
            return linkprovider;
        }
        /**
         * If no pageSize is set, the default value A4 is used.
         * @param pageSize the pageSize to set
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext SetPageSize(Rectangle pageSize) {
            this.pageSize = pageSize;
            return this;
        }

        /**
         * Create a clone of this HtmlPipelineContext, the clone only contains the
         * initial values, not the internal values. Beware, the state of the current
         * Context is not copied to the clone. Only the configurational important
         * stuff like the LinkProvider (same object), ImageProvider (new
         * {@link AbstractImageProvider} with same ImageRootPath) ,
         * TagProcessorFactory (same object), acceptUnknown (primitive), charset
         * (Charset.forName to get a new charset), autobookmark (primitive) are
         * copied.
         */
        public object Clone() {
            HtmlPipelineContext newCtx = new HtmlPipelineContext();
            if (this.imageProvider != null) {
                String rootPath =  imageProvider.GetImageRootPath();
                newCtx.SetImageProvider(new CloneImageProvider(rootPath));
            }
            if (null != this.charset) {
                newCtx.CharSet(Encoding.GetEncoding(this.charset.CodePage));
            }
            newCtx.SetPageSize(new Rectangle(this.pageSize)).SetLinkProvider(this.linkprovider)
                    .SetRootTags(new List<String>(this.roottags)).AutoBookmark(this.autoBookmark)
                    .SetTagFactory(this.tagFactory).SetAcceptUnknown(this.acceptUnknown);
            return newCtx;
        }

        private class CloneImageProvider : AbstractImageProvider {
            private string rootPath;

            public CloneImageProvider(string rootPath) {
                this.rootPath = rootPath;
            }

            public override String GetImageRootPath() {
                return rootPath;
            }
        }

        /**
         * Set to true to allow the HtmlPipeline to accept tags it does not find in
         * the given {@link TagProcessorFactory}
         *
         * @param acceptUnknown true or false
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext SetAcceptUnknown(bool acceptUnknown) {
            this.acceptUnknown = acceptUnknown;
            return this;
        }
        /**
         * Set the {@link TagProcessorFactory} to be used. For HTML use {@link Tags#getHtmlTagProcessorFactory()}
         * @param tagFactory the {@link TagProcessorFactory} that should be used
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext SetTagFactory(ITagProcessorFactory tagFactory) {
            this.tagFactory = tagFactory;
            return this;
        }

        /**
         * Set to true to enable the automatic creation of bookmarks on &lt;h1&gt;
         * to &lt;h6&gt; tags. Works in conjunction with {@link Header}.
         *
         * @param autoBookmark true or false
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext AutoBookmark(bool autoBookmark) {
            this.autoBookmark = autoBookmark;
            return this;
        }

        /**
         * Set the root-tags, this matters for margins. By default these are set to
         * &lt;body&gt; and &lt;div&gt;.
         *
         * @param roottags the root tags
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext SetRootTags(IList<String> roottags) {
            this.roottags = roottags;
            return this;
        }

        /**
         * An IImageProvide can be provided and works in conjunction with
         * {@link Image} and {@link ListStyleTypeCssApplier} for List Images.
         *
         * @param imageProvider the {@link IImageProvide} to use.
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext SetImageProvider(IImageProvider imageProvider) {
            this.imageProvider = imageProvider;
            return this;
        }

        /**
         * Set the ILinkProvider to use if any.
         *
         * @param linkprovider the ILinkProvider (@see
         *            {@link HtmlPipelineContext#getLinkProvider()}
         * @return this <code>HtmlPipelineContext</code>
         */
        public HtmlPipelineContext SetLinkProvider(ILinkProvider linkprovider) {
            this.linkprovider = linkprovider;
            return this;
        }
    }
}