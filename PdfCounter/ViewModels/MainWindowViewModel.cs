using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DynamicData;
using DynamicData.Binding;
using iText.Kernel.Pdf;
using PdfCounter.Models;
using ReactiveUI;

namespace PdfCounter.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private string _loadedJobName = "none";
    public string LoadedJobName
    {
        get => _loadedJobName;
        set => this.RaiseAndSetIfChanged(ref _loadedJobName, value);
    }
    private string _pdfFileName = string.Empty;
    public string PdfFileName
    {
        get => _pdfFileName;
        set => this.RaiseAndSetIfChanged(ref _pdfFileName, value);
    }
    private int _currentTab;
    public int CurrentTab
    {
        get => _currentTab;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentTab, value);
            if (value == 2 && Rows.Count > 0)
            {
                RowsNotZero = true;
            }
            else
            {
                RowsNotZero = false;
            }
        }
    }
    private PdfDocument? _pdfDocument;
    public void SetPdf(PdfDocument doc) => _pdfDocument = doc;
    private PdfReader? _pdfReader;
    private Stream? _pdfStream;
    private readonly IPdfExtractorService _pdfExtractor;
    private string _pdfPath = string.Empty;
    public string PdfPath
    {
        get => _pdfPath;
        set => this.RaiseAndSetIfChanged(ref _pdfPath, value);
    }
    private string? _error;
    public string? Error
    {
        get => _error;
        set => this.RaiseAndSetIfChanged(ref _error, value);
    }
    private bool _contentEnabled = false;
    public bool ContentEnabled
    {
        get => _contentEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _contentEnabled, value);
            this.RaisePropertyChanged(nameof(ContentDisabled)); // notify dependent property
        }
    }
    private bool ContentDisabled => !ContentEnabled;
    private ObservableAsPropertyHelper<bool> _isLoading = default!;
    public bool IsLoading => _isLoading.Value;
    private ChunkRow? _selectedChunk;
    public ChunkRow? SelectedChunk
    {
        get => _selectedChunk;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChunk, value);
            SeedFromSelectedChunk();
        }
    }
    public IEnumerable<object> PropertyChoices => _propertyChoices();
    private IEnumerable<object> _propertyChoices() =>
        new object[] { AddPropertyItem.Instance }.Concat(Fields);

    private object? _selectedComboItem;
    public object? SelectedComboItem
    {
        get => _selectedComboItem;
        set => this.RaiseAndSetIfChanged(ref _selectedComboItem, value);
    }
    private bool _rowsNotZero = false;
    public bool RowsNotZero
    {
        get => _rowsNotZero;
        set => this.RaiseAndSetIfChanged(ref _rowsNotZero, value);
    }
    public ExtractField? SelectedField
    {
        get => _selectedField;
        set => this.RaiseAndSetIfChanged(ref _selectedField, value);
    }
    private ExtractField? _selectedField;
    public SaveJobProperty? SaveJobProperty
    {
        get => _saveJobProperty;
        set => this.RaiseAndSetIfChanged(ref _saveJobProperty, value);
    }
    private SaveJobProperty? _saveJobProperty = new();
    public Interaction<Unit, IStorageFile?> PickPdf { get; } = new();
    public Interaction<ExtractField, FieldEditorOutcome> EditField { get; } = new();
    public Interaction<string, Unit> ShowError { get; } = new();
    public Interaction<string, Unit> ShowInfo { get; } = new();
    public Interaction<Unit, Unit> ManageFields { get; } = new();
    public Interaction<Unit, string> LoadJob { get; } = new();
    public Interaction<SaveJobProperty, SaveJobProperty?> SaveJob { get; } = new();
    public ReactiveCommand<Unit, Unit> ManageFieldsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> AddFieldCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> LoadJobCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveJobCommand { get; private set; } = null!;
    public ReactiveCommand<ExtractField, Unit> RemoveFieldCommand { get; private set; } = null!;
    public ReactiveCommand<ExtractField, Unit> RemoveUserFieldCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SelectFileCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ProcessPdfFilesCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ProcessCoreCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearJobCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> AddTestBoxCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> LoadPageCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SeedFromChunkCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearTestBoxesOverlaysCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExportResultsToCsvCommand { get; private set; } = null!;
    public ObservableCollection<ExtractField> Fields { get; } = new();
    private ObservableCollection<PdfProperty> _pdfDoc = new();

    public ObservableCollection<PdfProperty> PdfDoc
    {
        get => _pdfDoc;
        set => this.RaiseAndSetIfChanged(ref _pdfDoc, value);
    }
    public ObservableCollection<IDictionary<string, string>> Rows { get; } = new();
    public ObservableCollection<string> ColumnNames { get; } = new();
    public ObservableCollection<object> ResultRows { get; } = new();

    public ObservableCollection<OverlayBox> OverlayBoxes { get; private set; } = new();
    public ObservableCollection<ChunkRow> ChunkRows { get; private set; } = new();
    public ObservableCollection<ExtractField> UserFields { get; } = new();

    string _chunkFilter = "";
    public string ChunkFilter { get => _chunkFilter; set { this.RaiseAndSetIfChanged(ref _chunkFilter, value); RebuildChunkList(); } }

    bool _loadingPage;
    public int SamplePageNumber
    {
        get => SamplePageIndex + 1;
        set
        {
            var newIndex = Math.Max(0, value - 1); // clamp so index never goes negative
            if (newIndex != SamplePageIndex)
                SamplePageIndex = newIndex; // this raises both property changes
        }
    }
    int _samplePageIndex = 0; // user-selected page to list chunks for
    public int SamplePageIndex
    {
        get => _samplePageIndex;
        set
        {
            if (_samplePageIndex != value)
            {
                this.RaiseAndSetIfChanged(ref _samplePageIndex, value);
                this.RaisePropertyChanged(nameof(SamplePageNumber));
                _ = LoadPageAsyncSafe();
            }
        }
    }

    private async Task LoadPageAsyncSafe()
    {
        if (_loadingPage) return;
        try { _loadingPage = true; await LoadPageAsync(); }
        finally { _loadingPage = false; }
    }

    Bitmap? _pageBitmap;
    public Bitmap? PageBitmap
    {
        get => _pageBitmap;
        set
        {
            var old = _pageBitmap;
            _pageBitmap = value;
            old?.Dispose();
            _pageBitmap = value;
            this.RaisePropertyChanged(nameof(PageBitmap));
            this.RaisePropertyChanged(nameof(ImagePixelWidth));
            this.RaisePropertyChanged(nameof(ImagePixelHeight));
        }
    }
     public int ImagePixelWidth => PageBitmap?.PixelSize.Width ?? 0;
    public int ImagePixelHeight => PageBitmap?.PixelSize.Height ?? 0;
    public string NewFieldName { get; set; } = "";
    public string NewMatchValues { get; set; } = "";
    public bool NewFieldIsFirst { get; set; }
    private bool _newFieldIsInline;
    public bool NewFieldIsInline
    {
        get => _newFieldIsInline;
        set
        {
            this.RaiseAndSetIfChanged(ref _newFieldIsInline, value);
            this.RaisePropertyChanged(nameof(NewFieldIsNotInline));
        }
    }
    public bool NewFieldIsNotInline
    {
        get => !_newFieldIsInline;
        set
        {
            var desiredInline = !value;
            if (desiredInline != _newFieldIsInline)
            {
                NewFieldIsInline = desiredInline; 
            }
        }
    }
    public int NewFieldGroup { get; set; }
    public double NewX { get; set; }
    public double NewY { get; set; }
    public double NewW { get; set; }
    public double NewH { get; set; }

    private double _pageWidthPts, _pageHeightPts;
    private List<PositionedText> _chunks = new();
    private int _comboSelectedIndex = -1;
    public int ComboSelectedIndex
    {
        get => _comboSelectedIndex;
        set => this.RaiseAndSetIfChanged(ref _comboSelectedIndex, value);
    }
    public string? FirstIdFieldName { get; private set; }
    public IDictionary<string, int> FirstIdCounts { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public int FirstIdModeCount { get; private set; }

    public bool CanSetFirstPageIdentifier(ExtractField field) =>
        !Fields.Any(f => f != field && f.IsFirstPageIdentifier);

    private ObservableAsPropertyHelper<bool> _isBusy = default!;

    private int _busyCount;
    private void BeginBusy()
    {
        if (Interlocked.Increment(ref _busyCount) == 1)
            this.RaisePropertyChanged(nameof(IsBusy));
    }
    private void EndBusy()
    {
        if (Interlocked.Decrement(ref _busyCount) == 0)
            this.RaisePropertyChanged(nameof(IsBusy));
    }
    // Make IsBusy include this counter:
    public bool IsBusy => (_isBusy?.Value ?? false) || _busyCount > 0;

    public MainWindowViewModel() : this(new PdfExtractorService())
    {
        Init();
    }

    public MainWindowViewModel(IPdfExtractorService pdfExtractor)
    {
        _pdfExtractor = pdfExtractor ?? throw new ArgumentNullException(nameof(pdfExtractor));
        Init();
    }

    private void Init()
    {
        Fields.CollectionChanged += (_, __) =>
        this.RaisePropertyChanged(nameof(PropertyChoices));

        Fields
        .ToObservableChangeSet()
        .AutoRefresh(f => f.IsFirstPageIdentifier)
        .Subscribe(_ => this.RaisePropertyChanged(nameof(Fields)));

        AddFieldCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var temp = new ExtractField
            {
                Name = "",
                IsFirstPageIdentifier = false,
                IsInlineValue = false,
                MatchValues = "",
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0
            };

            var outcome = await EditField.Handle(temp);

            if (outcome.Result == FieldEditorResult.Save && outcome.Edited is { } e)
            {
                if (string.IsNullOrWhiteSpace(e.Name))
                {
                    await ShowError.Handle("Property Name is required.");
                    return;
                }
                Fields.Add(e);
            }
        });

        RemoveFieldCommand = ReactiveCommand.Create<ExtractField>(field =>
        {
            if (field is null) return;
            Fields.Remove(field);
        });

        RemoveUserFieldCommand = ReactiveCommand.Create<ExtractField>(field =>
        {
            if (field is null) return;
            UserFields.Remove(field);
            RebuildOverlays();
        });

        ManageFieldsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await ManageFields.Handle(Unit.Default);
        });

        SelectFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var file = await PickPdf.Handle(Unit.Default);
            if (file is null) 
            {
                return;
            }

            ClearJob();

            ContentEnabled = true;
            PdfFileName = file.Name;
            _pdfStream = await file.OpenReadAsync();
            _pdfReader = new PdfReader(_pdfStream);
            _pdfDocument = new PdfDocument(_pdfReader);
            SetPdf(_pdfDocument);

            using var tmp = new MemoryStream();
            _pdfStream.Position = 0;
            await _pdfStream.CopyToAsync(tmp);
            tmp.Position = 0;
            var tempPath = Path.Combine(Path.GetTempPath(), $"PdfCounter_{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(tempPath, tmp.ToArray());
            PdfPath = tempPath;
            await LoadPageAsync();
        });

        SeedFromChunkCommand = ReactiveCommand.Create(SeedFromSelectedChunk);

        ProcessCoreCommand = ReactiveCommand.CreateFromTask(ProcessPdfFilesAsync);

        ProcessPdfFilesCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            Error = null;

            if (_pdfDocument is null)
            {
                Error = "Please select a PDF before processing.";
                await ShowError.Handle(Error);
                return;
            }

            await Task.Yield();
            await ProcessCoreCommand.Execute();
            CurrentTab = 2;
        });

        ProcessPdfFilesCommand.ThrownExceptions
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async ex =>
            {
                Error = $"ProcessPdfFilesCommand error: {ex}";
                await ShowError.Handle(Error);
                return;
            });

        ClearJobCommand = ReactiveCommand.Create(ClearJob);
        LoadPageCommand = ReactiveCommand.CreateFromTask(LoadPageAsync);
        RefreshCommand = ReactiveCommand.Create(RebuildAll);
        AddTestBoxCommand = ReactiveCommand.CreateFromTask(async () => { await AddTestBox(); });
        ClearTestBoxesOverlaysCommand = ReactiveCommand.Create(ClearTestBoxesOverlays);
        ExportResultsToCsvCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await ExportResultsToCsv();
        });

        LoadJobCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                var jobName = await LoadJob.Handle(Unit.Default);
                if (string.IsNullOrWhiteSpace(jobName))
                    return;

                var pkg = await LoadJobFromDiskAsync(jobName);
                if (pkg is null)
                {
                    await ShowError.Handle($"Job \"{jobName}\" not found or invalid.");
                    return;
                }

                if (_pdfDocument is not null)
                {
                    ClearJob();
                }
                LoadedJobName = jobName;

                await ApplyJobAsync(pkg);

            }
            catch (Exception ex)
            {
                await ShowError.Handle("Load job failed: " + ex.Message);
            }
        });

        SaveJobCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var input = new SaveJobProperty { JobName = LoadedJobName ?? "" }; // default
            var outcome = await SaveJob.Handle(input);
            if (outcome is null) return; // canceled

            await SaveJobToDiskAsync(outcome.JobName);
        });

        _isLoading = ProcessCoreCommand
        .IsExecuting
        .ObserveOn(RxApp.MainThreadScheduler)
        .ToProperty(this, vm => vm.IsLoading, initialValue: false);

        var busyStreams = new[]
       {
            ProcessCoreCommand.IsExecuting,
            SelectFileCommand.IsExecuting,
            ProcessPdfFilesCommand.IsExecuting,
            LoadPageCommand.IsExecuting,
            // LoadJobCommand.IsExecuting,
            // SaveJobCommand.IsExecuting,
        };

        _isBusy = Observable
        .Merge(busyStreams)
        .StartWith(false)
        .ObserveOn(RxApp.MainThreadScheduler)
        .ToProperty(this, vm => vm.IsBusy, initialValue: false);

    }

    private async Task ProcessPdfFilesAsync()
    {
        if (_pdfDocument is null)
        {
            return;
        }

        var results = await Task.Run(() =>
            _pdfExtractor.ProcessPdf(_pdfDocument!, UserFields)
        );


        RxApp.MainThreadScheduler.Schedule(() =>
        {
            PdfDoc.Clear();
            foreach (var p in results.Item1)
                PdfDoc.Add(p);

            LoadMany(PdfDoc, results.totalDocuments, results.totalPages, results.totalBlankPages, results.allDocFonts);
            RebuildAll();
            RowsNotZero = Rows.Count > 1 && CurrentTab == 2 ? true : false;
        });
    }

    private void LoadMany(ObservableCollection<PdfProperty> props, int totalDocuments, int totalPages, int totalBlankPages, HashSet<string> allDocFonts)
    {
        Rows.Clear();
        ColumnNames.Clear();
        ColumnNames.Add("DocNo");
        RowsNotZero = false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DocNo", };

        foreach (var grp in props.GroupBy(p => p.DocNo).OrderBy(g => g.Key))
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DocNo"] = grp.Key.ToString(),
            };

            foreach (var p in grp)
            {
                row[p.Name] = p.Value ?? "";
                if (seen.Add(p.Name)) ColumnNames.Add(p.Name);
                row["DocStartingPage"] = p.DocStartingPage.ToString();
                if (seen.Add("DocStartingPage")) ColumnNames.Add("DocStartingPage");
                row["DocPages"] = p.DocPages.ToString();
                if (seen.Add("DocPages")) ColumnNames.Add("DocPages");
                row["DocBlankPages"] = p.DocBlankPages.ToString();
                if (seen.Add("DocBlankPages")) ColumnNames.Add("DocBlankPages");
                row["DocFonts"] = p.Fonts.ToString();
                if (seen.Add("DocFonts")) ColumnNames.Add("DocFonts");
            }

            foreach (var k in ColumnNames)
                if (!row.ContainsKey(k)) row[k] = "";

            Rows.Add(row);
        }

        var totalRows = new Dictionary<string, string>
        {
            ["DocNo"] = $"TOTAL {totalDocuments}",
            ["DocPages"] = $"TOTAL {totalPages}",
            ["DocBlankPages"] = $"TOTAL {totalBlankPages}",
            ["DocFonts"] = $"FOUND: {string.Join(", ", allDocFonts)}"
        };
        Rows.Insert(0, totalRows);
        RecomputeFirstIdStats();
    }

    private async Task ExportResultsToCsv()
    {
        if (Rows == null || Rows.Count == 0) return;

        var cols = (ColumnNames != null && ColumnNames.Count > 0)
            ? ColumnNames.ToList()
            : Rows.SelectMany(r => r.Keys).Distinct().ToList();

        var sb = new StringBuilder();

        static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var needsQuotes = s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            var t = s.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{t}\"" : t;
        }

        sb.AppendLine(string.Join(",", cols.Select(Esc)));

        var dataRows = Rows.AsEnumerable();

        foreach (var row in dataRows)
        {
            var cells = cols.Select(c => row.TryGetValue(c, out var v) ? Esc(v) : "");
            sb.AppendLine(string.Join(",", cells));
        }

        var baseName = string.IsNullOrWhiteSpace(LoadedJobName)
            ? PdfFileName 
            : LoadedJobName;

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "export";
        }

        var safeName = new string(baseName.Where(ch =>
            char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());

        if (string.IsNullOrEmpty(safeName)) safeName = "export";

        var fileName = $"{safeName}_audit_report_{DateTime.Now.ToString("ddMMyyyy_HH_mm_ss")}.csv";
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        if (!Directory.Exists(downloads))
        {
            downloads = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        var fullPath = Path.Combine(downloads, fileName);

        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);

        await ShowInfo.Handle($"CSV exported to: {fullPath}");
    }

    private void ClearJob()
    {
        if (_pdfDocument is null && UserFields is null)
        {
            return;
        }

        Fields.Clear();
        PdfFileName = "";
        _pdfDocument?.Close();
        _pdfDocument = null;
        _pdfReader?.Close();
        _pdfStream?.Dispose();
        Rows.Clear();
        ColumnNames.Clear();
        ClearTestBoxesOverlays();
        ChunkRows.Clear();
        PageBitmap = null;
        ContentEnabled = false;
        CurrentTab = 0;
        LoadedJobName = "none";
        SamplePageIndex = 0;
    }

    private void ClearTestBoxesOverlays()
    {
        OverlayBoxes.Clear();
        UserFields.Clear();
        NewFieldIsFirst = false;
        NewFieldIsInline = false;
        NewMatchValues = "";
        NewFieldName = "";
        NewH = 0;
        NewW = 0;
        NewX = 0;
        NewY = 0;

        this.RaisePropertyChanged(nameof(NewFieldName));
        this.RaisePropertyChanged(nameof(NewX));
        this.RaisePropertyChanged(nameof(NewW));
        this.RaisePropertyChanged(nameof(NewH));
        this.RaisePropertyChanged(nameof(NewY));
        this.RaisePropertyChanged(nameof(NewFieldIsFirst));
        this.RaisePropertyChanged(nameof(NewFieldIsInline));
        this.RaisePropertyChanged(nameof(NewMatchValues));
    }

    private async System.Threading.Tasks.Task LoadPageAsync()
    {
        // Load the selected page (SamplePageIndex) — you can also respect PageIndex for Tab 3
        if (string.IsNullOrWhiteSpace(PdfPath) || !File.Exists(PdfPath))
        {
            await ShowError.Handle("PDF file not found for this job.");
            return;
        }

        int pageCount;
        try
        {
            using var r = new iText.Kernel.Pdf.PdfReader(PdfPath);
            using var d = new iText.Kernel.Pdf.PdfDocument(r);
            pageCount = d.GetNumberOfPages();
        }
        catch (Exception ex)
        {
            await ShowError.Handle("Failed to read PDF: " + ex.Message);
            return;
        }

        var raster = await PdfRasterizer.RenderPageAsync(this, PdfPath, SamplePageIndex, dpi: 150);
        PageBitmap = raster.Bitmap;
        _pageWidthPts = raster.PageWidthPts;
        _pageHeightPts = raster.PageHeightPts;

        // Extract chunks with iText7
        _chunks = ExtractChunksWithIText(PdfPath, SamplePageIndex); 

        // Populate grid rows
        ResultRows.Clear();
        foreach (var r in ExtractChunksWithIText(PdfPath, SamplePageIndex))
            ResultRows.Add(r);

        RebuildChunkList();
        RebuildOverlays(); 
    }

    private void RebuildAll()
    {
        RebuildChunkList();
        RebuildOverlays();
    }

    private void RebuildChunkList()
    {
        ChunkRows.Clear();
        IEnumerable<PositionedText> source = _chunks;

        if (!string.IsNullOrWhiteSpace(ChunkFilter))
            source = source.Where(c => (c.Text ?? "").IndexOf(ChunkFilter, System.StringComparison.OrdinalIgnoreCase) >= 0);

        foreach (var c in source.OrderByDescending(c => c.Y).ThenBy(c => c.X))
            if (!string.IsNullOrWhiteSpace(c.Text))
            {
                ChunkRows.Add(new ChunkRow
                {
                    Text = c.Text ?? "",
                    X = c.X,
                    Y = c.Y,                
                    Width = c.Width,
                    Height = c.Height,
                    Bottom = c.Bottom       
                });
            }
    }

    private void RebuildOverlays()
    {
        if (PageBitmap is null || _pageWidthPts <= 0 || _pageHeightPts <= 0)
            return;

        var scaleX = (double)ImagePixelWidth / _pageWidthPts;
        var scaleY = (double)ImagePixelHeight / _pageHeightPts;

        OverlayBoxes.Clear();

        foreach (var f in UserFields)
        {
            var leftPx = f.X * scaleX;
            var widthPx = f.Width * scaleX;

            var topPts = f.Y + f.Height; // top edge in points
            var topPx = (_pageHeightPts - topPts) * scaleY; // flip to top-origin
            var heightPx = f.Height * scaleY;

            if (f.IsFirstPageIdentifier)
            {
                OverlayBoxes.Add(new OverlayBox
                {
                    Label = f.Name,
                    BorderBoxColor = "Red",
                    LeftPx = leftPx,
                    TopPx = topPx,
                    WidthPx = widthPx,
                    HeightPx = heightPx,
                });
            }
    
            if (!f.IsFirstPageIdentifier)
                {
                    OverlayBoxes.Add(new OverlayBox
                    {
                        Label = f.Name,
                        BorderBoxColor = "Green",
                        LeftPx = leftPx,
                        TopPx = topPx,
                        WidthPx = widthPx,
                        HeightPx = heightPx,
                    });
                }
        }
    }

    private async Task AddTestBox()
    {
        if (string.IsNullOrWhiteSpace(NewFieldName))
        {
            await ShowError.Handle($"Field name is required.");
            return;
        }

        if (UserFields.Any(f => f.Name.Trim().Equals(NewFieldName.Trim())))
        {
            await ShowError.Handle($"Overlay box {NewFieldName} has already been added.");
            return;
        }

        var hasBlankCoordinate =
            NewX <= 0 ||
            NewY <= 0 ||
            NewH <= 0 ||
            NewW <= 0;

        if (hasBlankCoordinate && !NewFieldIsInline && string.IsNullOrWhiteSpace(NewMatchValues))
        {
            await ShowError.Handle($"Values required for X, Y, Width and Height.");
            return;
        }

        if (NewFieldIsFirst && UserFields.Any(f => f.IsFirstPageIdentifier))
        {
            await ShowError.Handle($"First page identifier box already added.");
            return;
        }

        var f = new ExtractField
        {
            Name = string.IsNullOrWhiteSpace(NewFieldName) ? "" : NewFieldName.Trim(),
            IsFirstPageIdentifier = NewFieldIsFirst,
            IsInlineValue = NewFieldIsInline,
            MatchValues = NewMatchValues,
            X = NewX,
            Y = NewY,
            Width = NewW,
            Height = NewH,
            PageIndex = SamplePageIndex
        };

        UserFields.Add(f);
        RebuildOverlays();
    }

    private List<PositionedText> ExtractChunksWithIText(string? pdfPath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(pdfPath)) return new();

        using var reader = new PdfReader(pdfPath);
        using var doc = new PdfDocument(reader);
        if (pageIndex < 0 || pageIndex >= doc.GetNumberOfPages()) return new();

        var page = doc.GetPage(pageIndex + 1); // iText pages are 1-based
        var strat = new ExtractionStrategy();
        var proc = new iText.Kernel.Pdf.Canvas.Parser.PdfCanvasProcessor(strat);
        proc.ProcessPageContent(page);
        return strat.Chunks;
    }

    private void SeedFromSelectedChunk()
    {
        if (SelectedChunk is null) return;

        // X & Width are fine as-is
        NewFieldName = string.IsNullOrWhiteSpace(SelectedChunk.Text) ? "FromChunk" : SelectedChunk.Text;
        NewX = SelectedChunk.X;
        NewW = SelectedChunk.Width;
        NewH = SelectedChunk.Height;
        NewFieldIsFirst = false;
        NewFieldIsInline = false;
        NewMatchValues = "";

        NewY = SelectedChunk.Bottom != 0 ? SelectedChunk.Bottom
                                        : SelectedChunk.Y - (SelectedChunk.Height / 2.0);

        // Notify inputs in case your field editor binds to them
        this.RaisePropertyChanged(nameof(NewFieldName));
        this.RaisePropertyChanged(nameof(NewX));
        this.RaisePropertyChanged(nameof(NewW));
        this.RaisePropertyChanged(nameof(NewH));
        this.RaisePropertyChanged(nameof(NewY));
        this.RaisePropertyChanged(nameof(NewFieldIsFirst));
        this.RaisePropertyChanged(nameof(NewFieldIsInline));
        this.RaisePropertyChanged(nameof(NewMatchValues));

        CurrentTab = 1;
    }

    private void RecomputeFirstIdStats()
    {
        FirstIdFieldName = Fields.FirstOrDefault(f => f.IsFirstPageIdentifier)?.Name;

        FirstIdCounts.Clear();
        FirstIdModeCount = 0;

        if (string.IsNullOrWhiteSpace(FirstIdFieldName))
        {
            this.RaisePropertyChanged(nameof(FirstIdFieldName));
            this.RaisePropertyChanged(nameof(FirstIdCounts));
            this.RaisePropertyChanged(nameof(FirstIdModeCount));
            return;
        }

        foreach (var row in Rows.Skip(1))
        {
            if (row.TryGetValue(FirstIdFieldName, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                if (!FirstIdCounts.TryGetValue(v, out var n)) n = 0;
                FirstIdCounts[v] = n + 1;
            }
        }

        foreach (var kv in FirstIdCounts)
            if (kv.Value > FirstIdModeCount) FirstIdModeCount = kv.Value;

        this.RaisePropertyChanged(nameof(FirstIdFieldName));
        this.RaisePropertyChanged(nameof(FirstIdCounts));
        this.RaisePropertyChanged(nameof(FirstIdModeCount));
    }

    private async Task ApplyJobAsync(JobPackage job)
    {
        BeginBusy();
        try
        {
            OpenPdfFromPathAsync(job.PdfPath);
            PdfFileName = job.PdfFileName;
            var pageCount = _pdfDocument?.GetNumberOfPages() ?? 0;
            var idx = Math.Clamp(job.SamplePageIndex, 0, Math.Max(pageCount - 1, 0));

            _samplePageIndex = idx;
            this.RaisePropertyChanged(nameof(SamplePageIndex));
            ContentEnabled = true;

            UserFields.Clear();
            foreach (var f in job.Fields ?? new List<ExtractField>())
                UserFields.Add(f);

            await LoadPageAsync();
            RebuildOverlays();
            await ProcessPdfFilesAsync();

            CurrentTab = 2;
            SamplePageIndex = job.SamplePageIndex;
            RowsNotZero = Rows.Count > 1 && CurrentTab == 2;
        }
        catch (FileNotFoundException)
        {
            await ShowError.Handle($"PDF not found: {job.PdfPath}");
        }
        catch (IOException ex)
        {
            await ShowError.Handle($"Could not open PDF: {ex.Message}");
        }
        catch (Exception ex)
        {
            await ShowError.Handle($"Error applying job: {ex.Message}");   
        }
        finally
        {
            EndBusy();
        }
    }
    
    public async Task LoadJobByNameAsync(string jobName)
    {

        var job = await LoadJobFromDiskAsync(jobName);
        if (job == null)
        {
            await ShowError.Handle($"Job \"{jobName}\" not found.");
            return;
        }

        if (!File.Exists(job.PdfPath))
        {
            await ShowError.Handle($"Saved PDF path not found:\n{job.PdfPath}");
            return;
        }
        await ApplyJobAsync(job);
    }

    private async Task<JobPackage?> LoadJobFromDiskAsync(string jobName)
    {
        var fullPath = Path.Combine(GetJobsFolder(), jobName + ".json");
        if (!File.Exists(fullPath)) return null;

        var json = await File.ReadAllTextAsync(fullPath);
        return JsonSerializer.Deserialize<JobPackage>(json);
    }

    private async Task SaveJobToDiskAsync(string jobName)
    {
        var job = new JobPackage
        {
            Name = jobName,
            PdfFileName = PdfFileName,
            PdfPath = PdfPath,
            SamplePageIndex = SamplePageIndex,
            Fields = UserFields.ToList(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var fileName = SanitizeFileName(jobName) + ".json";
        var fullPath = Path.Combine(GetJobsFolder(), fileName);

        var json = JsonSerializer.Serialize(job, options);
        await File.WriteAllTextAsync(fullPath, json);

        await ShowInfo.Handle($"Saved job to: {fullPath}");
        LoadedJobName = jobName;
        Console.WriteLine($"Saved job to: {fullPath}");
    }

    private static string SanitizeFileName(string name, string fallback = "job")
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private static string GetJobsFolder()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(root, "PdfCounter", "Jobs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void OpenPdfFromPathAsync(string path)
    {
        PdfFileName = Path.GetFileName(path);

        _pdfDocument?.Close();
        _pdfReader?.Close();
        _pdfStream?.Dispose();

        _pdfStream = File.OpenRead(path);
        _pdfReader = new PdfReader(_pdfStream);
        _pdfDocument = new PdfDocument(_pdfReader);
        SetPdf(_pdfDocument);

        PdfPath = path;
    }
}

