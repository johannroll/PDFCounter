using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PdfCounter.Models;

public sealed class PdfExtractorService : IPdfExtractorService
{
    private sealed class DocSummary
    {
        public int StartPage;
        public int EndPage;
        public int BlankPages;
        public HashSet<string> Fonts = new(StringComparer.OrdinalIgnoreCase);
    }

    // ===== Units: ALL ExtractField coordinates are in PDF points (1/72") with BOTTOM-LEFT origin =====
    public (ObservableCollection<PdfProperty> results,
            int totalPages,
            int totalBlankPages,
            int totalDocuments,
            HashSet<string> allDocFonts)
    ProcessPdf(PdfDocument pdfDocument, ObservableCollection<ExtractField> fields)
    {
        if (pdfDocument is null)
            return (new ObservableCollection<PdfProperty>(), 0, 0, 0, new HashSet<string>());

        var results = new ObservableCollection<PdfProperty>();
        int totalPages = pdfDocument.GetNumberOfPages();
        int totalBlankPages = 0;

        var allDocFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int docNo = 0;
        var current = new DocSummary { StartPage = 1, EndPage = 0, BlankPages = 0 };
        var summaries = new Dictionary<int, DocSummary>();

        for (int pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var page = pdfDocument.GetPage(pageNum);
            var pageSize = page.GetPageSize();
            double pageHeightPts = pageSize.GetHeight(); // points, bottom-left origin

            var strategy = new ExtractionStrategy();
            var processor = new PdfCanvasProcessor(strategy);
            processor.ProcessPageContent(page);

            // Blank-page check via full text
            var raw  = PdfTextExtractor.GetTextFromPage(page) ?? string.Empty;
            var text = Regex.Replace(raw.Replace("\u00A0", " "), @"\s+", " ").Trim();

            bool isBlank = string.IsNullOrWhiteSpace(text);
            if (isBlank)
            {
                totalBlankPages++;
                if (docNo > 0) current.BlankPages++;
            }

            // Font collection
            foreach (var f in strategy.Fonts)
            {
                var clean = f.Replace(",", "");
                allDocFonts.Add(clean);
                if (docNo > 0) current.Fonts.Add(clean);
            }

            // Detect document boundary using any IsFirstPageIdentifier field that yields a value
            bool startsNewDoc = false;
            foreach (var idField in fields.Where(f => f.IsFirstPageIdentifier))
            {
                var v = ExtractValue(strategy, idField, pageHeightPts);
                if (!string.IsNullOrWhiteSpace(v))
                {
                    startsNewDoc = true;
                    break;
                }
            }

            if (startsNewDoc)
            {
                if (docNo > 0)
                {
                    current.EndPage = pageNum - 1;
                    summaries[docNo] = current;
                }

                docNo++;
                current = new DocSummary
                {
                    StartPage = pageNum,
                    EndPage = 0,
                    BlankPages = isBlank ? 1 : 0,
                    Fonts = new HashSet<string>(strategy.Fonts.Select(x => x.Replace(",", "")),
                                                StringComparer.OrdinalIgnoreCase)
                };
            }

            if (docNo == 0) continue;

            // Capture all fields
            foreach (var field in fields)
            {
                var value = ExtractValue(strategy, field, pageHeightPts);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    bool exists = results.Any(p =>
                        p.DocNo == docNo &&
                        p.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase) &&
                        p.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        results.Add(new PdfProperty
                        {
                            DocNo = docNo,
                            DocStartingPage = current.StartPage,
                            Name  = field.Name,
                            Value = value
                        });
                    }
                }
            }

            if (pageNum == totalPages && docNo > 0)
            {
                current.EndPage = current.EndPage == 0 ? pageNum : current.EndPage;
                summaries[docNo] = current;
            }
        }

        // Apply summaries to each row
        foreach (var r in results)
        {
            if (summaries.TryGetValue(r.DocNo, out var s))
            {
                r.DocPages = (s.EndPage >= s.StartPage) ? (s.EndPage - s.StartPage + 1) : 1;
                r.DocBlankPages = s.BlankPages;
                r.Fonts = string.Join(", ", s.Fonts);
            }
        }

        return (results, totalPages, totalBlankPages, docNo, allDocFonts);
    }

    // --- helpers ---

    private static string ExtractValue(ExtractionStrategy strategy, ExtractField field, double pageHeightPts)
    {
        // If Width/Height > 0 we treat X/Y/Width/Height as PDF points (bottom-left origin) and extract within the rectangle
        if (!field.IsInlineValue && string.IsNullOrWhiteSpace(field.MatchValues) && field.Width > 0 && field.Height > 0)
        {
            return ExtractFromRect(strategy, field);
        }

        // Otherwise use label-based extraction
        if (field.IsInlineValue)
        {
            return ExtractRightOfLabel(strategy, field.Name);
        }

        if (!string.IsNullOrWhiteSpace(field.MatchValues))
        {
            return ExtractMatchingValues(strategy, field.MatchValues);
        }

        return string.Empty;
    }

    /// Rect-based extraction where ExtractField coordinates/dimensions are in **PDF points**, bottom-left origin.
    private static string ExtractFromRect(ExtractionStrategy strategy, ExtractField f)
    {
        // User rectangle in **points**, bottom-left origin
        double left   = f.X;
        double right  = f.X + f.Width;
        double bottom = f.Y;
        double top    = f.Y + f.Height;

        // Small forgiveness to account for float noise (points)
        const double tolX = 0.50;
        const double tolY = 0.50;
        left   -= tolX; right  += tolX;
        bottom -= tolY; top    += tolY;

        // Thresholds: how much of the chunk must be covered by the user rect?
        const double minChunkOverlap = 0.60;   // 60% of the CHUNK's area must be inside user rect
        const double maxHeightDeltaPts   = 1.5; // absolute height slack in points
        const double maxHeightDeltaRatio = 0.25; // or 25% relative slack

        double rectH = top - bottom;

        var hits = strategy.Chunks
            .Where(c =>
            {
                // Chunk rectangle
                double cl = c.X;
                double cr = c.X + c.Width;
                double cb = c.Bottom;
                double ct = c.Top;

                // Intersections
                double iw = Math.Max(0, Math.Min(right, cr) - Math.Max(left, cl));
                double ih = Math.Max(0, Math.Min(top,   ct) - Math.Max(bottom, cb));
                if (iw <= 0 || ih <= 0) return false;

                double inter = iw * ih;
                double carea = Math.Max(0.0001, (cr - cl) * (ct - cb));

                // 1) Enough of the CHUNK is covered by the user rect?
                bool overlapOk = (inter / carea) >= minChunkOverlap;

                // 2) Height roughly consistent with the strip?
                //    (prevents very short runs that barely intersect)
                double hDelta = Math.Abs(c.Height - rectH);
                bool heightOk = hDelta <= Math.Max(maxHeightDeltaPts, maxHeightDeltaRatio * c.Height);

                return overlapOk && heightOk;
            })
            .OrderByDescending(c => c.Y) // line order: top to bottom
            .ThenBy(c => c.X)
            .Select(c => c.Text);

        return string.Join(" ", hits).Trim();
    }

    /// Label → nearest text chunk to the right on the same line (tolerance in **points**).
    private static string ExtractRightOfLabel(ExtractionStrategy strategy, string labelText)
    {
        if (string.IsNullOrWhiteSpace(labelText)) return string.Empty;

        var label = strategy.Chunks.FirstOrDefault(c =>
            c.Text.Equals(labelText, StringComparison.OrdinalIgnoreCase));

        if (label is null) return string.Empty;

        const double yTolPts = 2.0; // roughly ~0.7 mm

        var rightChunk = strategy.Chunks
            .Where(c => Math.Abs(c.Y - label.Y) < yTolPts && c.X > label.X + label.Width)
            .OrderBy(c => c.X)
            .FirstOrDefault();

        return rightChunk?.Text?.Trim() ?? string.Empty;
    }
    private static string ExtractMatchingValues(ExtractionStrategy strategy, string matchingValues)
    {
        if (string.IsNullOrWhiteSpace(matchingValues)) return string.Empty;

        static string Normalize(string? s) =>
            Regex.Replace(s ?? "", @"\s+", " ").Trim();

        var matchingValuesHashSet = matchingValues.Split(",")
            .Select(Normalize)
            // .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var label = strategy.Chunks.FirstOrDefault(c =>
            matchingValuesHashSet.Contains(Normalize(c.Text)));

        if (label is null) return string.Empty;

        return label.Text;
    }
}

// unchanged except we also expose Top/Bottom/Height already
    public class ExtractionStrategy : IEventListener
    {
        public HashSet<string> Fonts { get; } = new();
        public List<PositionedText> Chunks { get; } = new();

        public void EventOccurred(IEventData data, EventType type)
    {
        if (type != EventType.RENDER_TEXT) return;

        var info = (TextRenderInfo)data;

        // Baseline & direction
        var blStart = info.GetBaseline().GetStartPoint();
        var blEnd   = info.GetBaseline().GetEndPoint();

        float baseX = blStart.Get(0);
        float baseY = blStart.Get(1);
        float dirX  = blEnd.Get(0) - blStart.Get(0);
        float dirY  = blEnd.Get(1) - blStart.Get(1);
        float dirLen = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
        if (dirLen <= 0f) dirLen = 1f; // guard

        // Unit normal to baseline (perpendicular)
        float nX = -dirY / dirLen;
        float nY =  dirX / dirLen;

        // Width along baseline (may be 0 for single-glyph chunks)
        float width = dirX; // horizontal-ish PDFs
        // For robustness, compute scalar projection of (blEnd-blStart) on baseline:
        width = (dirX * (dirX / dirLen)) + (dirY * (dirY / dirLen)); // == dirLen

        // Try ascent/desc lines
        var asc  = info.GetAscentLine();
        var desc = info.GetDescentLine();

        bool ascOk  = asc != null && !float.IsNaN(asc.GetStartPoint().Get(0))  && !float.IsNaN(asc.GetStartPoint().Get(1))
                            && !float.IsNaN(asc.GetEndPoint().Get(0))       && !float.IsNaN(asc.GetEndPoint().Get(1));
        bool descOk = desc != null && !float.IsNaN(desc.GetStartPoint().Get(0)) && !float.IsNaN(desc.GetStartPoint().Get(1))
                            && !float.IsNaN(desc.GetEndPoint().Get(0))      && !float.IsNaN(desc.GetEndPoint().Get(1));

        float height, topY, bottomY, yMid;

        if (ascOk && descOk && asc is not null && desc is not null)
        {
            // Distance between lines along the normal (use both endpoints & take the larger for safety)
            var a1 = asc.GetStartPoint();  var a2 = asc.GetEndPoint();
            var d1 = desc.GetStartPoint(); var d2 = desc.GetEndPoint();

            float Dist(float ax, float ay, float dx, float dy)
                => Math.Abs((ax - dx) * nX + (ay - dy) * nY);

            float h1 = Dist(a1.Get(0), a1.Get(1), d1.Get(0), d1.Get(1));
            float h2 = Dist(a2.Get(0), a2.Get(1), d2.Get(0), d2.Get(1));
            height = Math.Max(h1, h2);

            // Top/Bottom Y (for your non-rotated overlay usage, keep using Y-extrema)
            float ascYmax = Math.Max(a1.Get(1), a2.Get(1));
            float descYmin = Math.Min(d1.Get(1), d2.Get(1));
            topY = Math.Max(ascYmax, descYmin + height);   // defensive
            bottomY = topY - height;
            yMid = (topY + bottomY) / 2f;
        }
        else
        {
            // === Metrics fallback (noisy PDFs / Type3 fonts, etc.) ===
            float fs = info.GetFontSize();
            float emFactor = 0.75f; // last-resort

            try
            {
                var fm = info.GetFont()?.GetFontProgram()?.GetFontMetrics();
                if (fm != null)
                {
                    // Units are per 1000 em
                    var ascU  = fm.GetTypoAscender();
                    var descU = Math.Abs(fm.GetTypoDescender());
                    if (ascU != 0 || descU != 0)
                        emFactor = (ascU + descU) / 1000f;
                    else if (fm.GetCapHeight() != 0)
                        emFactor = (fm.GetCapHeight() / 1000f) * 1.1f;
                    else if (fm.GetXHeight() != 0)
                        emFactor = (fm.GetXHeight() / 1000f) * 1.5f;
                }
            }
            catch { /* ignore, keep fallback */ }

            height = fs * emFactor;
            if (!(height > 0f)) height = 8f; // absolute last fallback

            // Place top/bottom along the baseline normal (works even if rotated)
            float topX = baseX + nX * (height * 0.6f);
            float topYp= baseY + nY * (height * 0.6f);
            float botX = topX - nX * height;
            float botYp= topYp - nY * height;

            topY    = Math.Max(topYp, botYp);
            bottomY = Math.Min(topYp, botYp);
            yMid    = (topY + bottomY) / 2f;
        }

        // Font name (unchanged)
        var fontProgram = info.GetFont().GetFontProgram();
        var rawName  = fontProgram.GetFontNames().GetFontName();
        var fontName = rawName.Contains('+') ? rawName.Split('+')[1] : rawName;
        Fonts.Add(fontName);

        var text = info.GetText() ?? string.Empty;
        if (text.Length == 0) return;

        // Guards
        if (float.IsNaN(baseX)) baseX = 0f;
        if (float.IsNaN(yMid))  yMid  = baseY;
        if (float.IsNaN(width) || width < 0f) width = 0f;
        if (float.IsNaN(height) || height < 0f) height = 0f;
        if (float.IsNaN(topY) || float.IsNaN(bottomY)) { topY = baseY + height * 0.5f; bottomY = baseY - height * 0.5f; }

        Chunks.Add(new PositionedText
        {
            Text   = text,
            X      = baseX,
            Y      = yMid,
            Width  = width,
            Top    = topY,
            Bottom = bottomY,
            Height = height
        });
    }

    public ICollection<EventType> GetSupportedEvents() => new[] { EventType.RENDER_TEXT };
}
