using System.Globalization;
using System.Reflection;
using PcCleaner.Core;

namespace PcCleaner.App;

/// <summary>
/// Tek pencereli arayüz: 1) başlık  2) tarama  3) sonuçlar + temizleme.
/// Arayüz bilinçli olarak kod ile kuruldu (Designer dosyası yok) → sade ve okunabilir.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    // --- Kontroller ---
    private readonly Button _scanButton = new() { Text = "Taramayı Başlat", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
    private readonly Button _cancelButton = new() { Text = "İptal", AutoSize = true, Enabled = false, Padding = new Padding(6, 3, 6, 3) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 18, Margin = new Padding(8, 8, 0, 0) };
    private readonly Label _statusLabel = new() { AutoSize = true, Text = "Taramaya hazır.", Margin = new Padding(0, 6, 0, 0) };

    private readonly GroupBox _resultsGroup = new() { Text = "Bulunan Dosyalar", Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        CheckBoxes = true,
        FullRowSelect = true,
        ShowItemToolTips = true,
        HideSelection = false,
    };
    private readonly Button _selectAllButton = new() { Text = "Tümünü Seç", AutoSize = true, Enabled = false };
    private readonly Button _selectNoneButton = new() { Text = "Seçimi Kaldır", AutoSize = true, Enabled = false };
    private readonly Label _selectedLabel = new() { AutoSize = true, Text = "Seçilen Boyut: 0 bayt", Margin = new Padding(12, 8, 0, 0) };
    private readonly Button _cleanButton = new() { Text = "Temizlemeyi Başlat", AutoSize = true, Enabled = false, Padding = new Padding(10, 3, 10, 3) };

    // --- Durum ---
    private IReadOnlyList<ApprovedLocation> _locations = [];
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _closeRequested;
    private bool _bulkCheckUpdate;
    private long _selectedBytes;
    private int _selectedCount;

    public MainForm()
    {
        Text = "PC CLEANER";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(640, 460);
        Padding = new Padding(12);
        LoadIcon();

        BuildLayout();

        _scanButton.Click += async (_, _) => await ScanAsync();
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _cleanButton.Click += async (_, _) => await CleanAsync();
        _selectAllButton.Click += (_, _) => SetAllChecked(true);
        _selectNoneButton.Click += (_, _) => SetAllChecked(false);
        _list.ItemChecked += OnItemChecked;
        FormClosing += OnFormClosing;
    }

    // ------------------------------------------------------------------ Arayüz

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 1. başlık
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 2. tarama
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 3. sonuçlar
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // alt bilgi

        // 1) Başlık ve açıklama
        var header = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = "PC CLEANER",
            AutoSize = true,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x0B, 0x5C, 0xB8),
        });
        header.Controls.Add(new Label
        {
            Text = "Yalnızca sizin kullanıcı hesabınıza ait geçici dosya klasörlerini tarar ve yalnızca sizin onayladığınız dosyaları siler.",
            AutoSize = true,
            MaximumSize = new Size(740, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 10),
        });
        root.Controls.Add(header, 0, 0);

        // 2) Tarama düğmesi + ilerleme
        var scanRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2 };
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scanRow.Controls.Add(_scanButton, 0, 0);
        scanRow.Controls.Add(_cancelButton, 1, 0);
        scanRow.Controls.Add(_progress, 2, 0);
        scanRow.Controls.Add(_statusLabel, 0, 1);
        scanRow.SetColumnSpan(_statusLabel, 3);
        _statusLabel.AutoEllipsis = true;
        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Height = 22;
        scanRow.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(scanRow, 0, 1);

        // 3) Sonuç listesi + temizleme
        _list.Columns.Add("Dosya", 380);
        _list.Columns.Add("Boyut", 90, HorizontalAlignment.Right);
        _list.Columns.Add("Son Değişiklik", 130);
        _list.Columns.Add("Durum", 120);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 5, Padding = new Padding(0, 6, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_selectAllButton, 0, 0);
        bottom.Controls.Add(_selectNoneButton, 1, 0);
        bottom.Controls.Add(_selectedLabel, 2, 0);
        bottom.Controls.Add(_cleanButton, 4, 0);
        _selectedLabel.Font = new Font(Font, FontStyle.Bold);

        _resultsGroup.Controls.Add(_list);
        _resultsGroup.Controls.Add(bottom);
        root.Controls.Add(_resultsGroup, 0, 2);

        // Alt bilgi: geliştirici + dürüst kapsam notu
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 6, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label
        {
            Text = $"Geliştirici: Furkan  ·  Sürüm {AppVersion}  ·  Not: Yalnızca onaylı geçici klasörler taranır; Windows'taki tüm geçici dosyalar bulunmayabilir.",
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8f),
        }, 0, 0);
        var about = new LinkLabel { Text = "Hakkında", AutoSize = true };
        about.LinkClicked += (_, _) => ShowAbout();
        footer.Controls.Add(about, 1, 0);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        AcceptButton = _scanButton;
    }

    private void LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PcCleaner.App.pccleaner.ico");
        if (stream is not null)
            Icon = new Icon(stream);
    }

    // ------------------------------------------------------------------ Tarama

    private async Task ScanAsync()
    {
        if (_busy) return;

        _locations = TempLocations.GetDefault();
        if (_locations.Count == 0)
        {
            MessageBox.Show(this, "Taranabilecek güvenli bir geçici klasör bulunamadı.", "PC CLEANER",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ClearResults();
        SetBusy(true);
        _progress.Style = ProgressBarStyle.Marquee;
        _statusLabel.Text = "Taranıyor...";

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.CurrentDirectory))
                _statusLabel.Text = $"Taranıyor... {p.FilesFound.ToString("N0", Tr)} dosya, {SizeFormatter.Format(p.BytesFound)}  —  {p.CurrentDirectory}";
        });

        try
        {
            var scanner = new Scanner();
            var locations = _locations;
            ScanResult result = await Task.Run(() => scanner.Scan(locations, progress, token), token);
            ShowResults(result);
        }
        catch (OperationCanceledException)
        {
            ClearResults();
            _statusLabel.Text = "Tarama iptal edildi. Hiçbir dosya silinmedi.";
        }
        catch (Exception ex)
        {
            ClearResults();
            _statusLabel.Text = "Tarama sırasında bir hata oluştu.";
            MessageBox.Show(this, $"Tarama tamamlanamadı:\n{ex.Message}", "PC CLEANER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = 0;
            EndOperation();
        }
    }

    private void ShowResults(ScanResult result)
    {
        PopulateList(result.Files, statusByPath: null);

        string found = $"Bulunan Dosyalar: {result.Files.Count.ToString("N0", Tr)}  ·  Tahmini temizlenebilir alan: {SizeFormatter.Format(result.TotalBytes)}";
        var skipped = new List<string>();
        if (result.SkippedRecent > 0) skipped.Add($"{result.SkippedRecent.ToString("N0", Tr)} yeni dosya (son 24 saat)");
        if (result.SkippedProtected > 0) skipped.Add($"{result.SkippedProtected.ToString("N0", Tr)} korumalı dosya");
        if (result.SkippedLinks > 0) skipped.Add($"{result.SkippedLinks.ToString("N0", Tr)} bağlantı");
        if (result.InaccessibleDirectories > 0) skipped.Add($"{result.InaccessibleDirectories.ToString("N0", Tr)} erişilemeyen öğe");

        _statusLabel.Text = skipped.Count == 0
            ? found
            : $"{found}  ·  Güvenlik için atlanan: {string.Join(", ", skipped)}";

        if (result.Files.Count == 0)
            _statusLabel.Text = "Silinmeye uygun geçici dosya bulunamadı." + (skipped.Count > 0 ? $"  (Atlanan: {string.Join(", ", skipped)})" : "");
    }

    private void PopulateList(IEnumerable<TempFile> files, IReadOnlyDictionary<string, DeleteOutcome>? statusByPath)
    {
        _bulkCheckUpdate = true;
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            _list.Groups.Clear();

            var groups = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
            var items = new List<ListViewItem>();
            foreach (var f in files)
            {
                if (!groups.TryGetValue(f.Location.RootPath, out var group))
                {
                    group = new ListViewGroup($"{f.Location.DisplayName}  —  {f.Location.RootPath}");
                    groups[f.Location.RootPath] = group;
                    _list.Groups.Add(group);
                }

                bool skipped = statusByPath is not null && statusByPath.ContainsKey(f.FullPath);
                var item = new ListViewItem(f.RelativePath, group)
                {
                    Tag = f,
                    Checked = !skipped,
                    ToolTipText = f.FullPath,
                };
                item.SubItems.Add(SizeFormatter.Format(f.Size));
                item.SubItems.Add(f.LastWriteTimeUtc.ToLocalTime().ToString("g", Tr));
                item.SubItems.Add(skipped ? DescribeOutcome(statusByPath![f.FullPath]) : "");
                if (skipped) item.ForeColor = SystemColors.GrayText;
                items.Add(item);
            }
            _list.Items.AddRange([.. items]);
        }
        finally
        {
            _list.EndUpdate();
            _bulkCheckUpdate = false;
        }

        RecalculateSelection();
        _resultsGroup.Text = $"Bulunan Dosyalar ({_list.Items.Count.ToString("N0", Tr)})";
    }

    private void ClearResults()
    {
        _bulkCheckUpdate = true;
        _list.Items.Clear();
        _list.Groups.Clear();
        _bulkCheckUpdate = false;
        _resultsGroup.Text = "Bulunan Dosyalar";
        RecalculateSelection();
    }

    // ------------------------------------------------------------------ Seçim

    private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_bulkCheckUpdate || e.Item.Tag is not TempFile f) return;
        _selectedCount += e.Item.Checked ? 1 : -1;
        _selectedBytes += e.Item.Checked ? f.Size : -f.Size;
        UpdateSelectionUi();
    }

    private void SetAllChecked(bool value)
    {
        _bulkCheckUpdate = true;
        _list.BeginUpdate();
        foreach (ListViewItem item in _list.Items)
            item.Checked = value;
        _list.EndUpdate();
        _bulkCheckUpdate = false;
        RecalculateSelection();
    }

    private void RecalculateSelection()
    {
        _selectedCount = 0;
        _selectedBytes = 0;
        foreach (ListViewItem item in _list.CheckedItems)
        {
            if (item.Tag is TempFile f)
            {
                _selectedCount++;
                _selectedBytes += f.Size;
            }
        }
        UpdateSelectionUi();
    }

    private void UpdateSelectionUi()
    {
        _selectedLabel.Text = $"Seçilen Boyut: {SizeFormatter.Format(_selectedBytes)} ({_selectedCount.ToString("N0", Tr)} dosya)";
        bool hasItems = _list.Items.Count > 0 && !_busy;
        _cleanButton.Enabled = hasItems && _selectedCount > 0;
        _selectAllButton.Enabled = hasItems;
        _selectNoneButton.Enabled = hasItems;
    }

    // ------------------------------------------------------------------ Temizleme

    private async Task CleanAsync()
    {
        if (_busy) return;

        var selected = _list.CheckedItems.Cast<ListViewItem>()
            .Select(i => i.Tag).OfType<TempFile>().ToList();
        if (selected.Count == 0) return;

        long bytes = selected.Sum(f => f.Size);
        var answer = MessageBox.Show(this,
            $"{selected.Count.ToString("N0", Tr)} dosya kalıcı olarak silinecek.\n" +
            $"Kazanılacak tahmini alan: {SizeFormatter.Format(bytes)}\n\n" +
            "Kullanımda olan veya erişilemeyen dosyalar atlanacaktır.\n" +
            "Bu işlem geri alınamaz. Devam etmek istiyor musunuz?",
            "Temizlemeyi Onayla",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            _statusLabel.Text = "Temizleme iptal edildi. Hiçbir dosya silinmedi.";
            return;
        }

        SetBusy(true);
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Minimum = 0;
        _progress.Maximum = selected.Count;
        _progress.Value = 0;
        _statusLabel.Text = "Temizleniyor...";

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var progress = new Progress<int>(done =>
        {
            _progress.Value = Math.Min(done, _progress.Maximum);
            _statusLabel.Text = $"Temizleniyor... {done.ToString("N0", Tr)} / {selected.Count.ToString("N0", Tr)}";
        });

        try
        {
            var cleaner = new Cleaner(_locations);
            CleanResult result = await Task.Run(() => cleaner.Delete(selected, progress, token));
            ShowCleanResult(result);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Temizleme sırasında bir hata oluştu.";
            MessageBox.Show(this, $"Temizleme tamamlanamadı:\n{ex.Message}", "PC CLEANER", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _progress.Value = 0;
            EndOperation();
        }
    }

    private void ShowCleanResult(CleanResult result)
    {
        // Silinenleri listeden çıkar; atlananları "Durum" sütunuyla bırak.
        var deleted = result.Items.Where(i => i.Outcome == DeleteOutcome.Deleted)
            .Select(i => i.File.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var skipped = result.Items.Where(i => i.Outcome is not (DeleteOutcome.Deleted or DeleteOutcome.NotFound))
            .ToDictionary(i => i.File.FullPath, i => i.Outcome, StringComparer.OrdinalIgnoreCase);
        var notFound = result.Items.Where(i => i.Outcome == DeleteOutcome.NotFound)
            .Select(i => i.File.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var remaining = _list.Items.Cast<ListViewItem>().Select(i => i.Tag).OfType<TempFile>()
            .Where(f => !deleted.Contains(f.FullPath) && !notFound.Contains(f.FullPath))
            .ToList();
        PopulateList(remaining, skipped);

        string summary =
            $"Silinen: {result.DeletedCount.ToString("N0", Tr)} dosya ({SizeFormatter.Format(result.DeletedBytes)})";
        _statusLabel.Text = $"İşlem Tamamlandı  ·  {summary}" +
            (skipped.Count > 0 ? $"  ·  Atlanan: {skipped.Count.ToString("N0", Tr)}" : "");

        var lines = new List<string> { summary };
        if (result.SkippedCount > 0)
        {
            lines.Add($"Atlanan: {result.SkippedCount.ToString("N0", Tr)} dosya");
            AddLine(lines, result, DeleteOutcome.InUse);
            AddLine(lines, result, DeleteOutcome.AccessDenied);
            AddLine(lines, result, DeleteOutcome.Changed);
            AddLine(lines, result, DeleteOutcome.Rejected);
            AddLine(lines, result, DeleteOutcome.NotFound);
        }
        if (result.Cancelled)
            lines.Add("\nİşlem sizin isteğinizle durduruldu; kalan dosyalara dokunulmadı.");

        MessageBox.Show(this, string.Join("\n", lines), "İşlem Tamamlandı",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void AddLine(List<string> lines, CleanResult result, DeleteOutcome outcome)
    {
        int count = result.CountOf(outcome);
        if (count > 0)
            lines.Add($"   • {DescribeOutcome(outcome)}: {count.ToString("N0", Tr)}");
    }

    private static string DescribeOutcome(DeleteOutcome outcome) => outcome switch
    {
        DeleteOutcome.Deleted => "Silindi",
        DeleteOutcome.InUse => "Kullanımda",
        DeleteOutcome.AccessDenied => "İzin yok",
        DeleteOutcome.NotFound => "Zaten silinmiş",
        DeleteOutcome.Rejected => "Güvenlik nedeniyle atlandı",
        DeleteOutcome.Changed => "Dosya değişmiş",
        _ => "Atlandı",
    };

    // ------------------------------------------------------------------ Genel

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _scanButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _list.Enabled = !busy;
        UpdateSelectionUi();
    }

    private void EndOperation()
    {
        _cts?.Dispose();
        _cts = null;
        SetBusy(false);
        if (_closeRequested)
            Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // İşlem sürerken kapatılırsa: önce güvenle durdur, sonra kapat.
        if (_busy)
        {
            e.Cancel = true;
            _closeRequested = true;
            _cts?.Cancel();
        }
    }

    private void ShowAbout()
    {
        var locations = TempLocations.GetDefault();
        string list = locations.Count == 0
            ? "   (bulunamadı)"
            : string.Join("\n", locations.Select(l => $"   • {l.DisplayName}\n      {l.RootPath}"));

        MessageBox.Show(this,
            $"PC CLEANER  ·  Sürüm {AppVersion}\n" +
            "Geliştirici: Furkan\n\n" +
            "Ücretsizdir. Reklam, hesap, internet bağlantısı ve veri toplama yoktur.\n" +
            "Tüm işlemler yalnızca bu bilgisayarda yapılır.\n\n" +
            "Taranan klasörler:\n" + list + "\n\n" +
            "Güvenlik için son 24 saatte değişen, salt okunur, sistem ve bağlantı (kısayol/junction) öğeleri atlanır.",
            "Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
