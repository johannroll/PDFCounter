using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using PdfCounter.ViewModels;
using PDFiumCore;

public static class PdfRasterizer
{
    static PdfRasterizer()
    {
        fpdfview.FPDF_InitLibrary();
        AppDomain.CurrentDomain.ProcessExit += (_, __) =>
        {
            try { fpdfview.FPDF_DestroyLibrary(); } catch { /* ignore */ }
        };
    }

    public sealed class RasterPageResult
    {
        public Bitmap? Bitmap { get; init; } = default!;
        public double PageWidthPts { get; init; }   // PDF points
        public double PageHeightPts { get; init; }  // PDF points
    }

    /// <summary>
    /// Renders a single PDF page to an Avalonia Bitmap (BGRA premultiplied).
    /// </summary>
    /// <param name="path">Absolute or relative PDF file path.</param>
    /// <param name="pageIndex">0-based page index.</param>
    /// <param name="dpi">Target DPI (150–200 is a good default).</param>
    public static Task<RasterPageResult> RenderPageAsync(MainWindowViewModel vm, string path, int pageIndex, int dpi = 150)
        => Task.Run(() =>
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("PDF not found", path);

            // Open document (null password)
            var doc = fpdfview.FPDF_LoadDocument(path, null);
            if (doc.__Instance == IntPtr.Zero)
                throw new InvalidOperationException("FPDF_LoadDocument failed (invalid/corrupt PDF?)");

            try
            {
                int pageCount = fpdfview.FPDF_GetPageCount(doc);
                if (pageIndex < 0 || pageIndex >= pageCount)
                    throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Valid range: 0..{pageCount - 1}");

                // Page size in points
                double wPt = 0, hPt = 0;
                if (fpdfview.FPDF_GetPageSizeByIndex(doc, pageIndex, ref wPt, ref hPt) == 0)
                    throw new InvalidOperationException("FPDF_GetPageSizeByIndex failed");

                // Pixel size at requested DPI (px = pt * dpi / 72)
                int widthPx = Math.Max(1, (int)Math.Round(wPt * dpi / 72.0));
                int heightPx = Math.Max(1, (int)Math.Round(hPt * dpi / 72.0));
                int stride = widthPx * 4; // BGRA

                var page = fpdfview.FPDF_LoadPage(doc, pageIndex);
                if (page.__Instance == IntPtr.Zero)
                    throw new InvalidOperationException("FPDF_LoadPage failed");

                try
                {
                    var buffer = new byte[stride * heightPx];
                    var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    try
                    {
                        var bmp = fpdfview.FPDFBitmapCreateEx(
                            widthPx, heightPx,
                            (int)FPDFBitmapFormat.BGRA,
                            handle.AddrOfPinnedObject(),
                            stride);

                        if (bmp.__Instance == IntPtr.Zero)
                            throw new InvalidOperationException("FPDFBitmap_CreateEx failed");

                        try
                        {
                            // White background
                            fpdfview.FPDFBitmapFillRect(bmp, 0, 0, widthPx, heightPx, 0xFFFFFFFF);

                            // Render flags: LCD text + annotations (adjust to taste)
                            const int renderFlags =
#if NET8_0_OR_GREATER
                                (int)RenderFlags.OptimizeTextForLcd;
#else
                                0x02 /*LCD_TEXT*/ | 0x01 /*ANNOT*/;
#endif
                
                            fpdfview.FPDF_RenderPageBitmap(
                                bmp, page,
                                0, 0, widthPx, heightPx,
                                0, renderFlags);
                        }
                        finally
                        {
                            fpdfview.FPDFBitmapDestroy(bmp);
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }

                    var wb = new WriteableBitmap(
                        new PixelSize(widthPx, heightPx),
                        new Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Premul);

                    using (var fb = wb.Lock())
                    {
                        Marshal.Copy(buffer, 0, fb.Address, buffer.Length);
                    }

                    return new RasterPageResult
                    {
                        Bitmap = wb,
                        PageWidthPts = wPt,
                        PageHeightPts = hPt
                    };
                }
                finally
                {
                    fpdfview.FPDF_ClosePage(page);
                }
            }
            catch (Exception ex)
            {
                vm.ShowError.Handle(ex.Message);
                return new RasterPageResult();
            }
            finally
            {
                fpdfview.FPDF_CloseDocument(doc);
            }
        });
}
