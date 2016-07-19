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
using iText.IO.Font;
using iText.IO.Util;
using iText.Kernel.Font;
using iText.Kernel.Numbering;
using iText.Layout.Element;
using iText.Layout.Layout;
using iText.Layout.Properties;

namespace iText.Layout.Renderer {
    public class ListRenderer : BlockRenderer {
        public ListRenderer(List modelElement)
            : base(modelElement) {
        }

        // TODO underlying should not be applied
        // https://jira.itextsupport.com/browse/SUP-952
        public override LayoutResult Layout(LayoutContext layoutContext) {
            if (!HasOwnProperty(Property.LIST_SYMBOLS_INITIALIZED)) {
                IList<IRenderer> symbolRenderers = new List<IRenderer>();
                int listItemNum = (int)this.GetProperty<int?>(Property.LIST_START, 1);
                for (int i = 0; i < childRenderers.Count; i++) {
                    if (childRenderers[i].GetModelElement() is ListItem) {
                        childRenderers[i].SetParent(this);
                        IRenderer currentSymbolRenderer = MakeListSymbolRenderer(listItemNum++, childRenderers[i]);
                        childRenderers[i].SetParent(null);
                        symbolRenderers.Add(currentSymbolRenderer);
                        LayoutResult listSymbolLayoutResult = currentSymbolRenderer.SetParent(this).Layout(layoutContext);
                        currentSymbolRenderer.SetParent(null);
                        if (listSymbolLayoutResult.GetStatus() != LayoutResult.FULL) {
                            return new LayoutResult(LayoutResult.NOTHING, null, null, this);
                        }
                    }
                }
                float maxSymbolWidth = 0;
                foreach (IRenderer symbolRenderer in symbolRenderers) {
                    maxSymbolWidth = Math.Max(maxSymbolWidth, symbolRenderer.GetOccupiedArea().GetBBox().GetWidth());
                }
                float? symbolIndent = modelElement.GetProperty<float?>(Property.LIST_SYMBOL_INDENT);
                listItemNum = 0;
                foreach (IRenderer childRenderer in childRenderers) {
                    childRenderer.DeleteOwnProperty(Property.MARGIN_LEFT);
                    childRenderer.SetProperty(Property.MARGIN_LEFT, childRenderer.GetProperty(Property.MARGIN_LEFT, (float?)0f
                        ) + maxSymbolWidth + (symbolIndent != null ? symbolIndent : 0f));
                    if (childRenderer.GetModelElement() is ListItem) {
                        IRenderer symbolRenderer_1 = symbolRenderers[listItemNum++];
                        ((ListItemRenderer)childRenderer).AddSymbolRenderer(symbolRenderer_1, maxSymbolWidth);
                    }
                }
            }
            return base.Layout(layoutContext);
        }

        public override IRenderer GetNextRenderer() {
            return new iText.Layout.Renderer.ListRenderer((List)modelElement);
        }

        protected internal override AbstractRenderer CreateSplitRenderer(int layoutResult) {
            AbstractRenderer splitRenderer = base.CreateSplitRenderer(layoutResult);
            splitRenderer.SetProperty(Property.LIST_SYMBOLS_INITIALIZED, true);
            return splitRenderer;
        }

        protected internal override AbstractRenderer CreateOverflowRenderer(int layoutResult) {
            AbstractRenderer overflowRenderer = base.CreateOverflowRenderer(layoutResult);
            overflowRenderer.SetProperty(Property.LIST_SYMBOLS_INITIALIZED, true);
            return overflowRenderer;
        }

        protected internal virtual IRenderer MakeListSymbolRenderer(int index, IRenderer renderer) {
            Object defaultListSymbol = renderer.GetProperty<Object>(Property.LIST_SYMBOL);
            if (defaultListSymbol is Text) {
                return new TextRenderer((Text)defaultListSymbol);
            }
            else {
                if (defaultListSymbol is Image) {
                    return new ImageRenderer((Image)defaultListSymbol);
                }
                else {
                    if (defaultListSymbol is ListNumberingType) {
                        ListNumberingType numberingType = (ListNumberingType)defaultListSymbol;
                        String numberText;
                        switch (numberingType) {
                            case ListNumberingType.DECIMAL: {
                                numberText = index.ToString();
                                break;
                            }

                            case ListNumberingType.ROMAN_LOWER: {
                                numberText = RomanNumbering.ToRomanLowerCase(index);
                                break;
                            }

                            case ListNumberingType.ROMAN_UPPER: {
                                numberText = RomanNumbering.ToRomanUpperCase(index);
                                break;
                            }

                            case ListNumberingType.ENGLISH_LOWER: {
                                numberText = EnglishAlphabetNumbering.ToLatinAlphabetNumberLowerCase(index);
                                break;
                            }

                            case ListNumberingType.ENGLISH_UPPER: {
                                numberText = EnglishAlphabetNumbering.ToLatinAlphabetNumberUpperCase(index);
                                break;
                            }

                            case ListNumberingType.GREEK_LOWER: {
                                numberText = GreekAlphabetNumbering.ToGreekAlphabetNumberLowerCase(index);
                                break;
                            }

                            case ListNumberingType.GREEK_UPPER: {
                                numberText = GreekAlphabetNumbering.ToGreekAlphabetNumberUpperCase(index);
                                break;
                            }

                            case ListNumberingType.ZAPF_DINGBATS_1: {
                                numberText = iText.IO.Util.JavaUtil.CharToString((char)(index + 171));
                                break;
                            }

                            case ListNumberingType.ZAPF_DINGBATS_2: {
                                numberText = iText.IO.Util.JavaUtil.CharToString((char)(index + 181));
                                break;
                            }

                            case ListNumberingType.ZAPF_DINGBATS_3: {
                                numberText = iText.IO.Util.JavaUtil.CharToString((char)(index + 191));
                                break;
                            }

                            case ListNumberingType.ZAPF_DINGBATS_4: {
                                numberText = iText.IO.Util.JavaUtil.CharToString((char)(index + 201));
                                break;
                            }

                            default: {
                                throw new InvalidOperationException();
                            }
                        }
                        Text textElement = new Text(renderer.GetProperty<String>(Property.LIST_SYMBOL_PRE_TEXT) + numberText + renderer
                            .GetProperty<String>(Property.LIST_SYMBOL_POST_TEXT));
                        IRenderer textRenderer;
                        // Be careful. There is a workaround here. For Greek symbols we first set a dummy font with document=null
                        // in order for the metrics to be taken into account correctly during layout.
                        // Then on draw we set the correct font with actual document in order for the font objects to be created.
                        if (numberingType == ListNumberingType.GREEK_LOWER || numberingType == ListNumberingType.GREEK_UPPER || numberingType
                             == ListNumberingType.ZAPF_DINGBATS_1 || numberingType == ListNumberingType.ZAPF_DINGBATS_2 || numberingType
                             == ListNumberingType.ZAPF_DINGBATS_3 || numberingType == ListNumberingType.ZAPF_DINGBATS_4) {
                            String constantFont = (numberingType == ListNumberingType.GREEK_LOWER || numberingType == ListNumberingType
                                .GREEK_UPPER) ? FontConstants.SYMBOL : FontConstants.ZAPFDINGBATS;
                            textRenderer = new _TextRenderer_187(constantFont, textElement);
                            try {
                                textRenderer.SetProperty(Property.FONT, PdfFontFactory.CreateFont(constantFont));
                            }
                            catch (System.IO.IOException) {
                            }
                        }
                        else {
                            textRenderer = new TextRenderer(textElement);
                        }
                        return textRenderer;
                    }
                    else {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        private sealed class _TextRenderer_187 : TextRenderer {
            public _TextRenderer_187(String constantFont, Text baseArg1)
                : base(baseArg1) {
                this.constantFont = constantFont;
            }

            public override void Draw(DrawContext drawContext) {
                try {
                    this.SetProperty(Property.FONT, PdfFontFactory.CreateFont(constantFont));
                }
                catch (System.IO.IOException) {
                }
                base.Draw(drawContext);
            }

            private readonly String constantFont;
        }
    }
}