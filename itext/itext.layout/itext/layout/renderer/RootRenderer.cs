/*

This file is part of the iText (R) project.
Copyright (c) 1998-2016 iText Group NV
Authors: Bruno Lowagie, Paulo Soares, et al.

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License version 3
as published by the Free Software Foundation with the addition of the
following permission added to Section 15 as permitted in Section 7(a):
FOR ANY PART OF THE COVERED WORK IN WHICH THE COPYRIGHT IS OWNED BY
ITEXT GROUP. ITEXT GROUP DISCLAIMS THE WARRANTY OF NON INFRINGEMENT
OF THIRD PARTY RIGHTS

This program is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
See the GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program; if not, see http://www.gnu.org/licenses or write to
the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
Boston, MA, 02110-1301 USA, or download the license from the following URL:
http://itextpdf.com/terms-of-use/

The interactive user interfaces in modified source and object code versions
of this program must display Appropriate Legal Notices, as required under
Section 5 of the GNU Affero General Public License.

In accordance with Section 7(b) of the GNU Affero General Public License,
a covered work must retain the producer line in every PDF that is created
or manipulated using iText.

You can be released from the requirements of the license by purchasing
a commercial license. Buying such a license is mandatory as soon as you
develop commercial activities involving the iText software without
disclosing the source code of your own applications.
These activities include: offering paid services to customers as an ASP,
serving PDFs on the fly in a web application, shipping iText with a closed
source product.

For more information, please contact iText Software Corp. at this
address: sales@itextpdf.com
*/
using System;
using System.Collections.Generic;
using iText.IO;
using iText.IO.Log;
using iText.Layout.Layout;
using iText.Layout.Properties;

namespace iText.Layout.Renderer {
    public abstract class RootRenderer : AbstractRenderer {
        protected internal bool immediateFlush = true;

        protected internal LayoutArea currentArea;

        protected internal int currentPageNumber;

        private IRenderer keepWithNextHangingRenderer;

        private LayoutResult keepWithNextHangingRendererLayoutResult;

        public override void AddChild(IRenderer renderer) {
            base.AddChild(renderer);
            if (currentArea == null) {
                UpdateCurrentArea(null);
            }
            // Static layout
            if (currentArea != null && !childRenderers.IsEmpty() && childRenderers[childRenderers.Count - 1] == renderer
                ) {
                childRenderers.JRemoveAt(childRenderers.Count - 1);
                ProcessWaitingKeepWithNextElement(renderer);
                IList<IRenderer> resultRenderers = new List<IRenderer>();
                LayoutResult result = null;
                LayoutArea storedArea = null;
                LayoutArea nextStoredArea = null;
                while (currentArea != null && renderer != null && (result = renderer.SetParent(this).Layout(new LayoutContext
                    (currentArea.Clone()))).GetStatus() != LayoutResult.FULL) {
                    if (result.GetStatus() == LayoutResult.PARTIAL) {
                        if (result.GetOverflowRenderer() is ImageRenderer) {
                            ((ImageRenderer)result.GetOverflowRenderer()).AutoScale(currentArea);
                        }
                        else {
                            ProcessRenderer(result.GetSplitRenderer(), resultRenderers);
                            if (nextStoredArea != null) {
                                currentArea = nextStoredArea;
                                currentPageNumber = nextStoredArea.GetPageNumber();
                                nextStoredArea = null;
                            }
                            else {
                                UpdateCurrentArea(result);
                            }
                        }
                    }
                    else {
                        if (result.GetStatus() == LayoutResult.NOTHING) {
                            if (result.GetOverflowRenderer() is ImageRenderer) {
                                if (currentArea.GetBBox().GetHeight() < ((ImageRenderer)result.GetOverflowRenderer()).imageHeight && !currentArea
                                    .IsEmptyArea()) {
                                    UpdateCurrentArea(result);
                                }
                                ((ImageRenderer)result.GetOverflowRenderer()).AutoScale(currentArea);
                            }
                            else {
                                if (currentArea.IsEmptyArea() && !(renderer is AreaBreakRenderer)) {
                                    if (true.Equals(result.GetOverflowRenderer().GetModelElement().GetProperty<bool?>(Property.KEEP_TOGETHER))
                                        ) {
                                        result.GetOverflowRenderer().GetModelElement().SetProperty(Property.KEEP_TOGETHER, false);
                                        ILogger logger = LoggerFactory.GetLogger(typeof(RootRenderer));
                                        logger.Warn(String.Format(LogMessageConstant.ELEMENT_DOES_NOT_FIT_AREA, "KeepTogether property will be ignored."
                                            ));
                                        if (storedArea != null) {
                                            nextStoredArea = currentArea;
                                            currentArea = storedArea;
                                            currentPageNumber = storedArea.GetPageNumber();
                                        }
                                        storedArea = currentArea;
                                    }
                                    else {
                                        result.GetOverflowRenderer().SetProperty(Property.FORCED_PLACEMENT, true);
                                        ILogger logger = LoggerFactory.GetLogger(typeof(RootRenderer));
                                        logger.Warn(String.Format(LogMessageConstant.ELEMENT_DOES_NOT_FIT_AREA, ""));
                                    }
                                    renderer = result.GetOverflowRenderer();
                                    continue;
                                }
                                storedArea = currentArea;
                                if (nextStoredArea != null) {
                                    currentArea = nextStoredArea;
                                    currentPageNumber = nextStoredArea.GetPageNumber();
                                    nextStoredArea = null;
                                }
                                else {
                                    UpdateCurrentArea(result);
                                }
                            }
                        }
                    }
                    renderer = result.GetOverflowRenderer();
                }
                // Keep renderer until next element is added for future keep with next adjustments
                if (renderer != null && true.Equals(renderer.GetProperty(Property.KEEP_WITH_NEXT))) {
                    if (true.Equals(renderer.GetProperty(Property.FORCED_PLACEMENT))) {
                        ILogger logger = LoggerFactory.GetLogger(typeof(RootRenderer));
                        logger.Warn(LogMessageConstant.ELEMENT_WAS_FORCE_PLACED_KEEP_WITH_NEXT_WILL_BE_IGNORED);
                        UpdateCurrentAreaAndProcessRenderer(renderer, resultRenderers, result);
                    }
                    else {
                        keepWithNextHangingRenderer = renderer;
                        keepWithNextHangingRendererLayoutResult = result;
                    }
                }
                else {
                    if (currentArea != null) {
                        UpdateCurrentAreaAndProcessRenderer(renderer, resultRenderers, result);
                    }
                }
            }
            else {
                if (positionedRenderers.Count > 0 && positionedRenderers[positionedRenderers.Count - 1] == renderer) {
                    int? positionedPageNumber = renderer.GetProperty<int?>(Property.PAGE_NUMBER);
                    if (positionedPageNumber == null) {
                        positionedPageNumber = currentPageNumber;
                    }
                    renderer.SetParent(this).Layout(new LayoutContext(new LayoutArea((int)positionedPageNumber, currentArea.GetBBox
                        ().Clone())));
                    if (immediateFlush) {
                        FlushSingleRenderer(renderer);
                        positionedRenderers.JRemoveAt(positionedRenderers.Count - 1);
                    }
                }
            }
        }

        // Drawing of content. Might need to rename.
        public virtual void Flush() {
            foreach (IRenderer resultRenderer in childRenderers) {
                FlushSingleRenderer(resultRenderer);
            }
            foreach (IRenderer resultRenderer_1 in positionedRenderers) {
                FlushSingleRenderer(resultRenderer_1);
            }
            childRenderers.Clear();
            positionedRenderers.Clear();
        }

        /// <summary>
        /// This method correctly closes the
        /// <see cref="RootRenderer"/>
        /// instance.
        /// There might be hanging elements, like in case of
        /// <see cref="iText.Layout.Properties.Property.KEEP_WITH_NEXT"/>
        /// is set to true
        /// and when no consequent element has been added. This method addresses such situations.
        /// </summary>
        public virtual void Close() {
            if (keepWithNextHangingRenderer != null) {
                keepWithNextHangingRenderer.SetProperty(Property.KEEP_WITH_NEXT, false);
                IRenderer rendererToBeAdded = keepWithNextHangingRenderer;
                keepWithNextHangingRenderer = null;
                AddChild(rendererToBeAdded);
            }
            if (!immediateFlush) {
                Flush();
            }
        }

        public override LayoutResult Layout(LayoutContext layoutContext) {
            throw new InvalidOperationException("Layout is not supported for root renderers.");
        }

        public virtual LayoutArea GetCurrentArea() {
            if (currentArea == null) {
                UpdateCurrentArea(null);
            }
            return currentArea;
        }

        protected internal abstract void FlushSingleRenderer(IRenderer resultRenderer);

        protected internal abstract LayoutArea UpdateCurrentArea(LayoutResult overflowResult);

        private void ProcessRenderer(IRenderer renderer, IList<IRenderer> resultRenderers) {
            AlignChildHorizontally(renderer, currentArea.GetBBox().GetWidth());
            if (immediateFlush) {
                FlushSingleRenderer(renderer);
            }
            else {
                resultRenderers.Add(renderer);
            }
        }

        private void UpdateCurrentAreaAndProcessRenderer(IRenderer renderer, IList<IRenderer> resultRenderers, LayoutResult
             result) {
            currentArea.GetBBox().SetHeight(currentArea.GetBBox().GetHeight() - result.GetOccupiedArea().GetBBox().GetHeight
                ());
            currentArea.SetEmptyArea(false);
            if (renderer != null) {
                ProcessRenderer(renderer, resultRenderers);
            }
            if (!immediateFlush) {
                childRenderers.AddAll(resultRenderers);
            }
        }

        private void ProcessWaitingKeepWithNextElement(IRenderer renderer) {
            if (keepWithNextHangingRenderer != null) {
                LayoutArea rest = currentArea.Clone();
                rest.GetBBox().SetHeight(rest.GetBBox().GetHeight() - keepWithNextHangingRendererLayoutResult.GetOccupiedArea
                    ().GetBBox().GetHeight());
                bool ableToProcessKeepWithNext = false;
                if (renderer.SetParent(this).Layout(new LayoutContext(rest)).GetStatus() != LayoutResult.NOTHING) {
                    // The area break will not be introduced and we are safe to place everything as is
                    UpdateCurrentAreaAndProcessRenderer(keepWithNextHangingRenderer, new List<IRenderer>(), keepWithNextHangingRendererLayoutResult
                        );
                    ableToProcessKeepWithNext = true;
                }
                else {
                    float originalElementHeight = keepWithNextHangingRendererLayoutResult.GetOccupiedArea().GetBBox().GetHeight
                        ();
                    IList<float> trySplitHeightPoints = new List<float>();
                    float delta = 35;
                    for (int i = 1; i <= 5 && originalElementHeight - delta * i > originalElementHeight / 2; i++) {
                        trySplitHeightPoints.Add(originalElementHeight - delta * i);
                    }
                    for (int i_1 = 0; i_1 < trySplitHeightPoints.Count && !ableToProcessKeepWithNext; i_1++) {
                        float curElementSplitHeight = trySplitHeightPoints[i_1];
                        LayoutArea firstElementSplitLayoutArea = currentArea.Clone();
                        firstElementSplitLayoutArea.GetBBox().SetHeight(curElementSplitHeight).MoveUp(currentArea.GetBBox().GetHeight
                            () - curElementSplitHeight);
                        LayoutResult firstElementSplitLayoutResult = keepWithNextHangingRenderer.SetParent(this).Layout(new LayoutContext
                            (firstElementSplitLayoutArea.Clone()));
                        if (firstElementSplitLayoutResult.GetStatus() == LayoutResult.PARTIAL) {
                            LayoutArea storedArea = currentArea;
                            UpdateCurrentArea(firstElementSplitLayoutResult);
                            LayoutResult firstElementOverflowLayoutResult = firstElementSplitLayoutResult.GetOverflowRenderer().Layout
                                (new LayoutContext(currentArea.Clone()));
                            if (firstElementOverflowLayoutResult.GetStatus() == LayoutResult.FULL) {
                                LayoutArea secondElementLayoutArea = currentArea.Clone();
                                secondElementLayoutArea.GetBBox().SetHeight(secondElementLayoutArea.GetBBox().GetHeight() - firstElementOverflowLayoutResult
                                    .GetOccupiedArea().GetBBox().GetHeight());
                                LayoutResult secondElementLayoutResult = renderer.SetParent(this).Layout(new LayoutContext(secondElementLayoutArea
                                    ));
                                if (secondElementLayoutResult.GetStatus() != LayoutResult.NOTHING) {
                                    ableToProcessKeepWithNext = true;
                                    currentArea = firstElementSplitLayoutArea;
                                    currentPageNumber = firstElementSplitLayoutArea.GetPageNumber();
                                    UpdateCurrentAreaAndProcessRenderer(firstElementSplitLayoutResult.GetSplitRenderer(), new List<IRenderer>(
                                        ), firstElementSplitLayoutResult);
                                    UpdateCurrentArea(firstElementSplitLayoutResult);
                                    UpdateCurrentAreaAndProcessRenderer(firstElementSplitLayoutResult.GetOverflowRenderer(), new List<IRenderer
                                        >(), firstElementOverflowLayoutResult);
                                }
                            }
                            if (!ableToProcessKeepWithNext) {
                                currentArea = storedArea;
                                currentPageNumber = storedArea.GetPageNumber();
                            }
                        }
                    }
                }
                if (!ableToProcessKeepWithNext && !currentArea.IsEmptyArea()) {
                    LayoutArea storedArea = currentArea;
                    UpdateCurrentArea(null);
                    LayoutResult firstElementLayoutResult = keepWithNextHangingRenderer.SetParent(this).Layout(new LayoutContext
                        (currentArea.Clone()));
                    if (firstElementLayoutResult.GetStatus() == LayoutResult.FULL) {
                        LayoutArea secondElementLayoutArea = currentArea.Clone();
                        secondElementLayoutArea.GetBBox().SetHeight(secondElementLayoutArea.GetBBox().GetHeight() - firstElementLayoutResult
                            .GetOccupiedArea().GetBBox().GetHeight());
                        LayoutResult secondElementLayoutResult = renderer.SetParent(this).Layout(new LayoutContext(secondElementLayoutArea
                            ));
                        if (secondElementLayoutResult.GetStatus() != LayoutResult.NOTHING) {
                            ableToProcessKeepWithNext = true;
                            UpdateCurrentAreaAndProcessRenderer(keepWithNextHangingRenderer, new List<IRenderer>(), keepWithNextHangingRendererLayoutResult
                                );
                        }
                    }
                    if (!ableToProcessKeepWithNext) {
                        currentArea = storedArea;
                        currentPageNumber = storedArea.GetPageNumber();
                    }
                }
                if (!ableToProcessKeepWithNext) {
                    ILogger logger = LoggerFactory.GetLogger(typeof(RootRenderer));
                    logger.Warn(LogMessageConstant.RENDERER_WAS_NOT_ABLE_TO_PROCESS_KEEP_WITH_NEXT);
                    UpdateCurrentAreaAndProcessRenderer(keepWithNextHangingRenderer, new List<IRenderer>(), keepWithNextHangingRendererLayoutResult
                        );
                }
                keepWithNextHangingRenderer = null;
                keepWithNextHangingRendererLayoutResult = null;
            }
        }
    }
}