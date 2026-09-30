using System.Drawing;

namespace LogicTrace;

public sealed class MainForm : Form
{
    private const int InlineEvidenceCardDefaultHeight = 420;
    private const int InlineEvidenceCardMinimumHeight = 240;
    private const int InlineEvidenceResizeGripHeight = 12;
    private readonly LogicTraceDocumentService _documentService = new();
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
    private readonly TextBox _projectRootText = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _documentPathText = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _analysisMetricLabel = NewStatusMetricLabel();
    private readonly ToolStripStatusLabel _evidenceMetricLabel = NewStatusMetricLabel();
    private readonly ToolStripStatusLabel _locMetricLabel = NewStatusMetricLabel();
    private readonly ToolStripStatusLabel _unresolvedMetricLabel = NewStatusMetricLabel();
    private readonly ToolStripMenuItem _saveMenu = new("儲存(&S)") { ShortcutKeys = Keys.Control | Keys.S, Enabled = false };
    private readonly ToolStripMenuItem _saveAsMenu = new("另存新檔(&A)") { Enabled = false };
    private readonly Dictionary<string, TreeNode> _targetNodes = new(StringComparer.OrdinalIgnoreCase);
    private LogicTraceDocument? _document;
    private string? _documentPath;
    private bool _dirty;

    public MainForm()
    {
        Text = "LogicTrace";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 680);
        Size = new Size(1320, 850);
        Font = new Font("Segoe UI", 9F);

        var menu = BuildMenu();
        var projectBar = BuildProjectRootBar();
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        split.Panel1.Controls.Add(_content);
        split.Panel2.Controls.Add(_tree);
        var status = new StatusStrip();
        status.Items.AddRange([_statusLabel, _analysisMetricLabel, _evidenceMetricLabel, _locMetricLabel, _unresolvedMetricLabel]);

        Controls.Add(split);
        Controls.Add(projectBar);
        Controls.Add(menu);
        Controls.Add(status);
        split.SplitterDistance = Math.Max(550, split.Width - 330);
        split.Panel1MinSize = 550;
        split.Panel2MinSize = 230;
        MainMenuStrip = menu;
        _tree.AfterSelect += (_, e) => ShowNode(e.Node?.Tag);
        FormClosing += OnFormClosing;
        ShowWelcome();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("檔案(&F)");
        var open = new ToolStripMenuItem("開啟 JSON(&O)") { ShortcutKeys = Keys.Control | Keys.O };
        open.Click += (_, _) => OpenDocument();
        _saveMenu.Click += (_, _) => SaveDocument(false);
        _saveAsMenu.Click += (_, _) => SaveDocument(true);
        var exit = new ToolStripMenuItem("結束(&X)");
        exit.Click += (_, _) => Close();
        file.DropDownItems.AddRange([open, new ToolStripSeparator(), _saveMenu, _saveAsMenu, new ToolStripSeparator(), exit]);
        menu.Items.Add(file);
        return menu;
    }

    private Control BuildProjectRootBar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 74,
            Padding = new Padding(8, 6, 8, 6),
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.FromArgb(245, 247, 250)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.Controls.Add(new Label { Text = "Project Root", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 5, 12, 0) }, 0, 0);
        panel.Controls.Add(_projectRootText, 1, 0);
        var browse = new Button { Text = "瀏覽…", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        browse.Click += (_, _) => BrowseProjectRoot();
        panel.Controls.Add(browse, 2, 0);
        panel.Controls.Add(new Label { Text = "JSON File", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 5, 12, 0) }, 0, 1);
        panel.Controls.Add(_documentPathText, 1, 1);
        var open = new Button { Text = "瀏覽…", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        open.Click += (_, _) => OpenDocument();
        panel.Controls.Add(open, 2, 1);
        return panel;
    }

    private void BrowseProjectRoot()
    {
        using var dialog = new FolderBrowserDialog { Description = "選擇 Source Code 的 Project Root", UseDescriptionForTitle = true };
        if (Directory.Exists(_projectRootText.Text)) dialog.InitialDirectory = _projectRootText.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _projectRootText.Text = dialog.SelectedPath;
            _statusLabel.Text = "Project Root 已更新。";
            if (_tree.SelectedNode?.Tag is Evidence evidence) ShowEvidence(evidence);
        }
    }

    private void OpenDocument()
    {
        if (!ConfirmDiscardChanges()) return;
        using var dialog = new OpenFileDialog { Filter = "LogicTrace JSON (*.json)|*.json|所有檔案 (*.*)|*.*", Title = "開啟 LogicTrace JSON" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var result = _documentService.Load(dialog.FileName);
        if (!result.Success)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, result.Errors), "無法開啟 LogicTrace", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ApplyLoadedDocument(result.Document!, dialog.FileName);
        _statusLabel.Text = result.Warnings.Count == 0 ? $"已開啟 {Path.GetFileName(dialog.FileName)}" : $"已開啟；{result.Warnings.Count} 個資料警告。";
        if (result.Warnings.Count > 0)
            MessageBox.Show(this, string.Join(Environment.NewLine, result.Warnings), "資料警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ApplyLoadedDocument(LogicTraceDocument document, string path)
    {
        _document = document;
        _documentPath = path;
        _documentPathText.Text = Path.GetFullPath(path);
        _analysisMetricLabel.Text = $"Analysis {document.Feature.Metrics.AnalysisCoverage:0.##}%";
        _evidenceMetricLabel.Text = $"Evidence {document.Feature.Metrics.EvidenceLocCoverage:0.##}%";
        _locMetricLabel.Text = $"LOC {document.Feature.Metrics.TotalEffectiveLoc:N0}";
        _unresolvedMetricLabel.Text = $"Unresolved {document.Feature.Metrics.UnresolvedWeight:0.##}%";
        _analysisMetricLabel.Visible = _evidenceMetricLabel.Visible = _locMetricLabel.Visible = _unresolvedMetricLabel.Visible = true;
        _dirty = false;
        _saveMenu.Enabled = _saveAsMenu.Enabled = true;
        Text = $"LogicTrace — {_document.Feature.Name}";
        var jsonDirectory = Path.GetDirectoryName(path)!;
        if (string.IsNullOrWhiteSpace(_projectRootText.Text) && Directory.Exists(jsonDirectory)) _projectRootText.Text = jsonDirectory;
        BuildTree();
        _tree.SelectedNode = _tree.Nodes[0];
        _tree.Nodes[0].Expand();
    }

    internal void LoadDocumentForTest(string path)
    {
        var result = _documentService.Load(path);
        if (!result.Success || result.Document is null) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        _projectRootText.Text = Path.GetDirectoryName(path)!;
        ApplyLoadedDocument(result.Document, path);
    }

    internal void RunUiSmokeTest()
    {
        CreateControlTree(this);
        if (_document is null || _tree.Nodes.Count != 1) throw new InvalidOperationException("MainForm 未建立 Feature Tree。");
        ShowFeature(_document.Feature);
        var inlineEvidence = Descendants(_content).OfType<FlowLayoutPanel>().Single(control => Equals(control.Tag, "OverviewEvidenceHost"));
        ShowOverviewEvidence(_document.Feature.Overview.References[0], inlineEvidence);
        if (inlineEvidence.Controls.Count < 2)
            throw new InvalidOperationException("Overview Link 未在原頁面顯示 Evidence。");
        var evidenceWidth = inlineEvidence.Controls.OfType<TableLayoutPanel>().First().Width;
        ShowOverviewEvidence(_document.Feature.Overview.References[0], inlineEvidence);
        var repeatedEvidenceWidth = inlineEvidence.Controls.OfType<TableLayoutPanel>().First().Width;
        if (repeatedEvidenceWidth != evidenceWidth)
            throw new InvalidOperationException($"重複點擊 Overview Link 導致 Evidence 寬度改變：{evidenceWidth} → {repeatedEvidenceWidth}。");
        var resizableCard = inlineEvidence.Controls.OfType<TableLayoutPanel>().First();
        if (resizableCard.Height < InlineEvidenceCardDefaultHeight || !Descendants(resizableCard).Any(control => control.Cursor == Cursors.SizeNS))
            throw new InvalidOperationException("Overview Evidence 未建立可調整高度的拖曳把手。");
        var hostHeight = inlineEvidence.Height;
        resizableCard.Height += 100;
        ResizeInlineEvidenceHost(inlineEvidence);
        if (inlineEvidence.Height <= hostHeight)
            throw new InvalidOperationException("調整 Evidence 高度後，外層區域未同步更新。");
        foreach (var reference in _document.Feature.Overview.References)
        {
            if (!_targetNodes.TryGetValue(Key(reference.TargetType, reference.TargetId), out var node))
                throw new InvalidOperationException($"UI 找不到 Overview Target：{reference.TargetId}");
            _tree.SelectedNode = node;
            ShowNode(node.Tag);
            if (!ReferenceEquals(_tree.SelectedNode, node) || _content.Controls.Count == 0)
                throw new InvalidOperationException($"UI 無法導覽 Overview Target：{reference.TargetId}");
        }

        var rule = _document.LogicSections.SelectMany(section => section.Rules).First(item => item.VerificationStatus == VerificationStatus.Unverified);
        _tree.SelectedNode = _targetNodes[Key(ReferenceTargetType.Rule, rule.Id)];
        ShowRule(rule);
        CreateControlTree(_content);
        var combo = Descendants(_content).OfType<ComboBox>().Single();
        combo.SelectedItem = VerificationStatus.Verified;
        Application.DoEvents();
        if (rule.VerificationStatus != VerificationStatus.Verified || !_dirty)
            throw new InvalidOperationException("Verification Status UI 未正確更新 Model。");
    }

    private static void CreateControlTree(Control parent)
    {
        parent.CreateControl();
        foreach (Control child in parent.Controls) CreateControlTree(child);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private void BuildTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        _targetNodes.Clear();
        if (_document is null) return;
        var featureNode = new TreeNode(_document.Feature.Name) { Tag = _document.Feature };
        _tree.Nodes.Add(featureNode);
        foreach (var section in _document.LogicSections.OrderBy(s => s.Sequence))
        {
            var sectionNode = new TreeNode($"{section.Sequence}. {section.Title}") { Tag = section };
            featureNode.Nodes.Add(sectionNode);
            _targetNodes[Key(ReferenceTargetType.LogicSection, section.Id)] = sectionNode;
            foreach (var rule in section.Rules.OrderBy(r => r.Sequence))
            {
                var ruleNode = new TreeNode($"{rule.Sequence}. {rule.Title}") { Tag = rule };
                sectionNode.Nodes.Add(ruleNode);
                _targetNodes[Key(ReferenceTargetType.Rule, rule.Id)] = ruleNode;
                foreach (var evidence in rule.Evidences)
                {
                    var evidenceNode = new TreeNode($"證據 {evidence.Id}") { Tag = evidence, ForeColor = Color.DimGray };
                    ruleNode.Nodes.Add(evidenceNode);
                    _targetNodes[Key(ReferenceTargetType.Evidence, evidence.Id)] = evidenceNode;
                }
            }
        }
        if (_document.UnresolvedItems.Count > 0)
        {
            var unresolvedRoot = new TreeNode($"Unresolved ({_document.UnresolvedItems.Count})") { Tag = _document.UnresolvedItems };
            featureNode.Nodes.Add(unresolvedRoot);
            foreach (var item in _document.UnresolvedItems) unresolvedRoot.Nodes.Add(new TreeNode(item.Title) { Tag = item });
        }
        _tree.EndUpdate();
    }

    private static string Key(ReferenceTargetType type, string id) => $"{type}:{id}";

    private void ShowNode(object? item)
    {
        switch (item)
        {
            case Feature feature: ShowFeature(feature); break;
            case LogicSection section: ShowSection(section); break;
            case Rule rule: ShowRule(rule); break;
            case Evidence evidence: ShowEvidence(evidence); break;
            case List<UnresolvedItem> items: ShowUnresolved(items); break;
            case UnresolvedItem unresolved: ShowUnresolved([unresolved]); break;
        }
    }

    private FlowLayoutPanel NewPage(string title, string? subtitle = null, bool prominent = false)
    {
        _content.Controls.Clear();
        var page = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(28, 22, 28, 28),
            BackColor = Color.White
        };
        page.SizeChanged += (_, _) => ResizePageChildren(page);
        page.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", prominent ? 24F : 20F, FontStyle.Bold), ForeColor = Color.FromArgb(25, 50, 82), Margin = new Padding(0, 0, 0, 5) });
        if (!string.IsNullOrWhiteSpace(subtitle)) page.Controls.Add(new Label { Text = subtitle, AutoSize = true, MaximumSize = new Size(1000, 0), ForeColor = prominent ? Color.FromArgb(70, 82, 98) : Color.DimGray, Font = new Font("Segoe UI", prominent ? 11F : 10F), Margin = new Padding(0, 0, 0, prominent ? 22 : 16) });
        _content.Controls.Add(page);
        return page;
    }

    private void ResizePageChildren(FlowLayoutPanel page)
    {
        var availableWidth = Math.Max(400, _content.ClientSize.Width - page.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
        var width = availableWidth;
        foreach (Control control in page.Controls)
        {
            if (control is Label { AutoSize: true }) continue;
            control.Width = width;
        }
    }

    private void ShowWelcome()
    {
        NewPage("LogicTrace", "讀取已產生的 LogicTrace JSON，從整體功能一路下鑽到規則與 Source Code Evidence。");
        _statusLabel.Text = "請選擇 Project Root 並開啟 LogicTrace JSON。";
    }

    private void ShowFeature(Feature feature)
    {
        var page = NewPage(feature.Name, feature.Purpose, prominent: true);
        page.Controls.Add(BuildCollapsibleFeatureInfo(feature));
        page.Controls.Add(BuildFlowAndSections(feature.MainFlow));
        var inlineEvidence = BuildInlineEvidenceHost();
        page.Controls.Add(BuildOverviewCard(feature.Overview, reference => ShowOverviewEvidence(reference, inlineEvidence)));
        page.Controls.Add(inlineEvidence);
        if (_document!.UnresolvedItems.Count > 0)
        {
            page.Controls.Add(SectionHeading("Unresolved Items"));
            page.Controls.Add(BodyLabel(string.Join(Environment.NewLine, _document.UnresolvedItems.Select(i => $"• {i.Title} — {i.EstimatedWeight:0.##}%"))));
        }
        ResizePageChildren(page);
    }

    private static Control BuildCollapsibleFeatureInfo(Feature feature)
    {
        const int collapsedHeight = 38;
        var detailsHeight = Math.Max(134, 60 + Math.Max(2, feature.Components.Count) * 24);
        var expandedHeight = collapsedHeight + detailsHeight;
        var container = new Panel
        {
            Height = collapsedHeight,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 12)
        };
        var details = BuildFeatureInfoCard(feature, detailsHeight);
        details.Dock = DockStyle.Fill;
        details.Visible = false;
        details.Margin = Padding.Empty;
        var toggle = new Button
        {
            Text = "▶  基本資訊",
            Dock = DockStyle.Top,
            Height = collapsedHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 70, 90),
            BackColor = Color.FromArgb(247, 248, 250),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Padding = new Padding(8, 0, 0, 0)
        };
        toggle.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 222);
        toggle.Click += (_, _) =>
        {
            details.Visible = !details.Visible;
            container.Height = details.Visible ? expandedHeight : collapsedHeight;
            toggle.Text = details.Visible ? "▼  基本資訊" : "▶  基本資訊";
            container.Parent?.PerformLayout();
        };
        container.Controls.Add(details);
        container.Controls.Add(toggle);
        return container;
    }

    private Control BuildOverviewCard(Overview overview, Action<OverviewReference> onReferenceClick)
    {
        var link = BuildOverviewLinks(overview, onReferenceClick);
        link.Dock = DockStyle.Fill;
        link.Margin = Padding.Empty;
        var card = NewCard(136, primary: true);
        card.Controls.Add(link, 0, 1);
        card.Controls.Add(CardHeading("功能概述"), 0, 0);
        return card;
    }

    private static FlowLayoutPanel BuildInlineEvidenceHost()
    {
        var host = new FlowLayoutPanel
        {
            Tag = "OverviewEvidenceHost",
            AutoSize = false,
            Height = 0,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Visible = false,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 12)
        };
        host.SizeChanged += (_, _) =>
        {
            var width = Math.Max(300, host.ClientSize.Width);
            foreach (Control child in host.Controls)
            {
                if (child is not Label { AutoSize: true }) child.Width = width;
            }
        };
        return host;
    }

    private void ShowOverviewEvidence(OverviewReference reference, FlowLayoutPanel host)
    {
        if (_document is null) return;
        var items = new List<(string Context, Evidence Evidence)>();
        var title = reference.Text;
        switch (reference.TargetType)
        {
            case ReferenceTargetType.LogicSection:
                var section = _document.LogicSections.FirstOrDefault(item => item.Id.Equals(reference.TargetId, StringComparison.OrdinalIgnoreCase));
                if (section is not null)
                {
                    title = section.Title;
                    items.AddRange(section.Rules.SelectMany(rule => rule.Evidences.Select(evidence => (rule.Title, evidence))));
                }
                break;
            case ReferenceTargetType.Rule:
                var rule = _document.LogicSections.SelectMany(item => item.Rules).FirstOrDefault(item => item.Id.Equals(reference.TargetId, StringComparison.OrdinalIgnoreCase));
                if (rule is not null)
                {
                    title = rule.Title;
                    items.AddRange(rule.Evidences.Select(evidence => (rule.Title, evidence)));
                }
                break;
            case ReferenceTargetType.Evidence:
                var match = _document.LogicSections
                    .SelectMany(sectionItem => sectionItem.Rules.SelectMany(ruleItem => ruleItem.Evidences.Select(evidence => (Rule: ruleItem, Evidence: evidence))))
                    .FirstOrDefault(item => item.Evidence.Id.Equals(reference.TargetId, StringComparison.OrdinalIgnoreCase));
                if (match.Evidence is not null) items.Add((match.Rule.Title, match.Evidence));
                break;
        }

        host.SuspendLayout();
        host.Controls.Clear();
        host.Controls.Add(new Label
        {
            Text = $"已選取：{reference.Text}",
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 91, 171),
            Margin = new Padding(0, 6, 0, 2)
        });
        host.Controls.Add(new Label
        {
            Text = $"Evidence · {reference.TargetType} · {title}",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 8)
        });
        if (items.Count == 0)
        {
            host.Controls.Add(BodyLabel("此項目目前沒有 Evidence。"));
        }
        else
        {
            foreach (var item in items) host.Controls.Add(BuildInlineEvidenceCard(item.Context, item.Evidence));
        }
        var contentWidth = Math.Max(300, host.ClientSize.Width);
        foreach (Control child in host.Controls)
        {
            if (child is not Label { AutoSize: true }) child.Width = contentWidth;
        }
        host.Visible = true;
        host.ResumeLayout(true);
        ResizeInlineEvidenceHost(host);
    }

    private Control BuildInlineEvidenceCard(string context, Evidence evidence)
    {
        var card = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 5,
            Height = InlineEvidenceCardDefaultHeight,
            MinimumSize = new Size(0, InlineEvidenceCardMinimumHeight),
            BackColor = Color.FromArgb(249, 250, 252),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, InlineEvidenceResizeGripHeight));
        card.Controls.Add(new Label { Text = $"{context}  ·  {evidence.Id}", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 61, 91), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var open = new Button { Text = "完整檢視", AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        open.Click += (_, _) =>
        {
            if (_targetNodes.TryGetValue(Key(ReferenceTargetType.Evidence, evidence.Id), out var node)) _tree.SelectedNode = node;
        };
        card.Controls.Add(open, 1, 0);
        card.Controls.Add(new Label { Text = $"{evidence.FilePath}:{evidence.StartLine}-{evidence.EndLine}  ·  {evidence.ClassName}.{evidence.MethodName}", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        card.SetColumnSpan(card.GetControlFromPosition(0, 1)!, 2);
        card.Controls.Add(new Label { Text = evidence.Reason, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(55, 60, 68), TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        card.SetColumnSpan(card.GetControlFromPosition(0, 2)!, 2);
        var source = BuildInlineSourceViewer(evidence);
        card.Controls.Add(source, 0, 3);
        card.SetColumnSpan(source, 2);
        var resizeGrip = BuildInlineEvidenceResizeGrip(card);
        card.Controls.Add(resizeGrip, 0, 4);
        card.SetColumnSpan(resizeGrip, 2);
        return card;
    }

    private static Control BuildInlineEvidenceResizeGrip(Control card)
    {
        var grip = new Panel
        {
            Dock = DockStyle.Fill,
            Cursor = Cursors.SizeNS,
            BackColor = Color.FromArgb(235, 238, 242),
            Margin = Padding.Empty,
            AccessibleName = "調整 Evidence 高度",
            AccessibleDescription = "上下拖曳以調整程式碼顯示區域高度"
        };
        grip.Paint += (_, e) =>
        {
            var center = grip.ClientSize.Width / 2;
            using var pen = new Pen(Color.FromArgb(145, 153, 164));
            e.Graphics.DrawLine(pen, center - 18, 4, center + 18, 4);
            e.Graphics.DrawLine(pen, center - 18, 7, center + 18, 7);
        };

        var dragStartY = 0;
        var dragStartHeight = 0;
        grip.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            dragStartY = Cursor.Position.Y;
            dragStartHeight = card.Height;
            grip.Capture = true;
        };
        grip.MouseMove += (_, _) =>
        {
            if (!grip.Capture) return;
            var height = Math.Max(InlineEvidenceCardMinimumHeight, dragStartHeight + Cursor.Position.Y - dragStartY);
            if (height == card.Height) return;
            card.Height = height;
            if (card.Parent is FlowLayoutPanel host) ResizeInlineEvidenceHost(host);
        };
        grip.MouseUp += (_, _) => grip.Capture = false;
        return grip;
    }

    private static void ResizeInlineEvidenceHost(FlowLayoutPanel host)
    {
        host.PerformLayout();
        host.Height = host.Controls.Cast<Control>().Sum(control => control.Height + control.Margin.Vertical);
        host.Parent?.PerformLayout();
    }

    private Control BuildInlineSourceViewer(Evidence evidence)
    {
        var source = SourceFileReader.Read(_projectRootText.Text, evidence);
        if (!source.Success)
        {
            return new Label
            {
                Text = source.Error,
                Dock = DockStyle.Fill,
                ForeColor = Color.Firebrick,
                BackColor = Color.FromArgb(255, 245, 245),
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        var firstLine = Math.Max(1, evidence.StartLine - 3);
        var lastLine = Math.Min(source.Lines.Length, evidence.EndLine + 3);
        var displayedLines = source.Lines
            .Skip(firstLine - 1)
            .Take(lastLine - firstLine + 1)
            .Select((line, index) => $"{firstLine + index,5}  {line}")
            .ToArray();
        var viewer = new RichTextBox
        {
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5F),
            BackColor = Color.FromArgb(248, 249, 251),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Both,
            Text = string.Join(Environment.NewLine, displayedLines)
        };
        var relativeStartLine = evidence.StartLine - firstLine;
        var relativeEndLine = evidence.EndLine - firstLine;
        var start = viewer.GetFirstCharIndexFromLine(relativeStartLine);
        var end = relativeEndLine + 1 < displayedLines.Length ? viewer.GetFirstCharIndexFromLine(relativeEndLine + 1) : viewer.TextLength;
        if (start >= 0 && end >= start)
        {
            viewer.Select(start, end - start);
            viewer.SelectionBackColor = Color.FromArgb(255, 244, 190);
            viewer.Select(0, 0);
        }
        return viewer;
    }

    private static Control BuildFeatureInfoCard(Feature feature, int height)
    {
        var card = NewCard(height, 2);
        card.ColumnStyles.Clear();
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        card.RowCount = 2;
        card.RowStyles.Clear();
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(CardSubheading("進入點"), 0, 0);
        card.Controls.Add(CardSubheading("使用元件"), 1, 0);
        card.Controls.Add(new Label
        {
            Text = $"{feature.EntryPoint.FilePath}{Environment.NewLine}{feature.EntryPoint.ClassName}.{feature.EntryPoint.MethodName}",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(45, 55, 68),
            Padding = new Padding(0, 4, 18, 0)
        }, 0, 1);
        card.Controls.Add(new Label
        {
            Text = string.Join(Environment.NewLine, feature.Components.Select(component => $"• {component}")),
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(45, 55, 68),
            Padding = new Padding(0, 4, 0, 0)
        }, 1, 1);
        return card;
    }

    private Control BuildFlowAndSections(IReadOnlyList<string> steps)
    {
        var height = Math.Max(230, 58 + steps.Count * 44);
        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Height = height,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var flow = BuildMainFlowCard(steps);
        flow.Dock = DockStyle.Fill;
        flow.Margin = new Padding(0, 0, 6, 0);
        layout.Controls.Add(flow, 0, 0);

        var sections = NewCard(height, primary: true);
        sections.Dock = DockStyle.Fill;
        sections.Margin = new Padding(6, 0, 0, 0);
        sections.Controls.Add(CardHeading("主要邏輯"), 0, 0);
        var grid = BuildSectionGrid(compact: true);
        grid.Dock = DockStyle.Fill;
        grid.Margin = Padding.Empty;
        sections.Controls.Add(grid, 0, 1);
        layout.Controls.Add(sections, 1, 0);
        return layout;
    }

    private static Control BuildMainFlowCard(IReadOnlyList<string> steps)
    {
        var card = NewCard(Math.Max(230, 58 + steps.Count * 44), primary: true);
        card.RowCount = steps.Count + 1;
        card.RowStyles.Clear();
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        card.Controls.Add(CardHeading("Main Flow"), 0, 0);
        for (var i = 0; i < steps.Count; i++)
        {
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            card.Controls.Add(new Label
            {
                Text = $"{i + 1}.  {steps[i]}",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(45, 55, 68),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, i + 1);
        }
        return card;
    }

    private static TableLayoutPanel NewCard(int height, int columns = 1, bool primary = false)
    {
        var card = new TableLayoutPanel
        {
            ColumnCount = columns,
            RowCount = 2,
            Height = height,
            BackColor = primary ? Color.FromArgb(239, 246, 255) : Color.FromArgb(249, 250, 252),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(15, 12, 15, 12),
            Margin = new Padding(0, 0, 0, 12)
        };
        for (var i = 0; i < columns; i++) card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return card;
    }

    private static Label CardHeading(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        ForeColor = Color.FromArgb(32, 48, 70),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label CardSubheading(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(70, 82, 98),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private Control BuildOverviewLinks(Overview overview, Action<OverviewReference> onReferenceClick)
    {
        var link = new LinkLabel
        {
            Text = overview.Text,
            AutoSize = false,
            Height = 80,
            Font = new Font("Segoe UI", 11F),
            LinkColor = Color.FromArgb(24, 91, 171),
            ActiveLinkColor = Color.FromArgb(180, 50, 50),
            LinkBehavior = LinkBehavior.HoverUnderline,
            Margin = new Padding(0, 2, 0, 10)
        };
        var occupied = new List<(int Start, int End)>();
        foreach (var reference in overview.References)
        {
            var searchAt = 0;
            while (searchAt < overview.Text.Length)
            {
                var index = overview.Text.IndexOf(reference.Text, searchAt, StringComparison.Ordinal);
                if (index < 0) break;
                var end = index + reference.Text.Length;
                if (!occupied.Any(x => index < x.End && end > x.Start))
                {
                    link.Links.Add(index, reference.Text.Length, reference);
                    occupied.Add((index, end));
                    break;
                }
                searchAt = end;
            }
        }
        link.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is OverviewReference reference) onReferenceClick(reference);
            else MessageBox.Show(this, "找不到這個 Overview Reference 的目標。", "顯示失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        return link;
    }

    private Control BuildSectionGrid(bool compact = false)
    {
        var grid = NewGrid();
        grid.Height = Math.Max(120, 38 + _document!.LogicSections.Count * 30);
        grid.Columns.Add("Title", "Logic Section");
        grid.Columns.Add("Weight", "Logic Weight");
        grid.Columns.Add("Share", "Code Share");
        grid.Columns.Add("Coverage", "Coverage");
        grid.Columns.Add("Loc", "Effective LOC");
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns[1].Width = compact ? 82 : 125;
        grid.Columns[2].Width = compact ? 82 : 125;
        grid.Columns[3].Width = compact ? 78 : 115;
        grid.Columns[4].Width = compact ? 86 : 125;
        foreach (var section in _document.LogicSections.OrderBy(s => s.Sequence))
        {
            var index = grid.Rows.Add(section.Title, $"{section.Metrics.LogicWeight:0.##}%", $"{section.Metrics.CodeShare:0.##}%", $"{section.Metrics.Coverage:0.##}%", section.Metrics.EffectiveLoc.ToString("N0"));
            grid.Rows[index].Tag = section;
        }
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is LogicSection section && _targetNodes.TryGetValue(Key(ReferenceTargetType.LogicSection, section.Id), out var node)) _tree.SelectedNode = node;
        };
        return grid;
    }

    private void ShowSection(LogicSection section)
    {
        var page = NewPage($"{section.Sequence}. {section.Title}", section.Description);
        page.Controls.Add(BuildMetrics([
            ("Logic Weight", $"{section.Metrics.LogicWeight:0.##}%"),
            ("Code Share", $"{section.Metrics.CodeShare:0.##}%"),
            ("Coverage", $"{section.Metrics.Coverage:0.##}%"),
            ("Effective LOC", section.Metrics.EffectiveLoc.ToString("N0"))
        ]));
        page.Controls.Add(SectionHeading("Rules"));
        var grid = NewGrid();
        grid.Height = Math.Max(110, 38 + section.Rules.Count * 30);
        grid.Columns.Add("Rule", "Rule");
        grid.Columns.Add("Category", "Category");
        grid.Columns.Add("Evidence", "Evidence Level");
        grid.Columns.Add("Confidence", "Confidence");
        grid.Columns.Add("Verification", "Verification");
        foreach (var rule in section.Rules.OrderBy(r => r.Sequence))
        {
            var index = grid.Rows.Add(rule.Title, rule.Category, rule.EvidenceLevel, rule.Confidence, rule.VerificationStatus);
            grid.Rows[index].Tag = rule;
        }
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is Rule rule && _targetNodes.TryGetValue(Key(ReferenceTargetType.Rule, rule.Id), out var node)) _tree.SelectedNode = node;
        };
        page.Controls.Add(grid);
        ResizePageChildren(page);
    }

    private void ShowRule(Rule rule)
    {
        var page = NewPage(rule.Title, rule.Description);
        page.Controls.Add(BuildMetrics([
            ("Category", rule.Category),
            ("Evidence Level", rule.EvidenceLevel.ToString()),
            ("Confidence", rule.Confidence.ToString()),
            ("Analysis Status", rule.AnalysisStatus.ToString())
        ]));
        page.Controls.Add(SectionHeading("人工驗證"));
        var verificationPanel = new FlowLayoutPanel { AutoSize = true, Height = 42, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 12) };
        verificationPanel.Controls.Add(new Label { Text = "Verification Status", AutoSize = true, Margin = new Padding(0, 8, 12, 0), Font = new Font(Font, FontStyle.Bold) });
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        combo.DataSource = Enum.GetValues<VerificationStatus>();
        combo.SelectedItem = rule.VerificationStatus;
        combo.SelectedValueChanged += (_, _) =>
        {
            if (combo.SelectedItem is VerificationStatus status && status != rule.VerificationStatus)
            {
                rule.VerificationStatus = status;
                SetDirty();
            }
        };
        verificationPanel.Controls.Add(combo);
        page.Controls.Add(verificationPanel);
        page.Controls.Add(SectionHeading($"Evidence ({rule.Evidences.Count})"));
        if (rule.Evidences.Count == 0) page.Controls.Add(BodyLabel("此 Rule 尚無 Evidence。"));
        foreach (var evidence in rule.Evidences)
        {
            var button = new Button
            {
                Text = $"{evidence.Id}   {evidence.FilePath}:{evidence.StartLine}-{evidence.EndLine}{Environment.NewLine}{evidence.Reason}",
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 64,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 8),
                Tag = evidence
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 222);
            button.Click += (_, _) =>
            {
                if (_targetNodes.TryGetValue(Key(ReferenceTargetType.Evidence, evidence.Id), out var node)) _tree.SelectedNode = node;
                else ShowEvidence(evidence);
            };
            page.Controls.Add(button);
        }
        ResizePageChildren(page);
    }

    private void ShowEvidence(Evidence evidence)
    {
        var page = NewPage($"Evidence {evidence.Id}", evidence.Reason);
        page.Controls.Add(BuildMetrics([
            ("File", evidence.FilePath),
            ("Class", evidence.ClassName),
            ("Method", evidence.MethodName),
            ("Lines", $"{evidence.StartLine}-{evidence.EndLine}")
        ]));
        page.Controls.Add(SectionHeading("Source Code"));
        var source = SourceFileReader.Read(_projectRootText.Text, evidence);
        page.Controls.Add(new Label { Text = source.FullPath ?? evidence.FilePath, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 6) });
        if (!source.Success)
        {
            var error = BodyLabel(source.Error);
            error.ForeColor = Color.Firebrick;
            page.Controls.Add(error);
            ResizePageChildren(page);
            return;
        }

        var viewer = new RichTextBox
        {
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = false,
            Height = 440,
            Font = new Font("Consolas", 10F),
            BackColor = Color.FromArgb(248, 249, 251),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Both,
            Text = string.Join(Environment.NewLine, source.Lines.Select((line, index) => $"{index + 1,5}  {line}"))
        };
        var start = viewer.GetFirstCharIndexFromLine(evidence.StartLine - 1);
        var end = evidence.EndLine < source.Lines.Length ? viewer.GetFirstCharIndexFromLine(evidence.EndLine) : viewer.TextLength;
        if (start >= 0 && end >= start)
        {
            viewer.Select(start, end - start);
            viewer.SelectionBackColor = Color.FromArgb(255, 244, 190);
            viewer.Select(start, 0);
            viewer.ScrollToCaret();
        }
        page.Controls.Add(viewer);
        ResizePageChildren(page);
    }

    private void ShowUnresolved(IReadOnlyCollection<UnresolvedItem> items)
    {
        var page = NewPage("Unresolved Items", "尚未盤點完整、需要後續調查的區域。");
        foreach (var item in items)
        {
            var box = new Panel { Height = 100, BackColor = Color.FromArgb(255, 249, 230), Margin = new Padding(0, 0, 0, 10), Padding = new Padding(14) };
            box.Controls.Add(new Label { Text = item.Title, Dock = DockStyle.Top, AutoSize = false, Height = 25, Font = new Font(Font, FontStyle.Bold) });
            box.Controls.Add(new Label { Text = $"{item.Reason}{Environment.NewLine}Estimated Weight: {item.EstimatedWeight:0.##}%   Related Section: {item.RelatedSectionId}", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 70, 45) });
            page.Controls.Add(box);
        }
        ResizePageChildren(page);
    }

    private static DataGridView NewGrid() => new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        Margin = new Padding(0, 0, 0, 12)
    };

    private static ToolStripStatusLabel NewStatusMetricLabel() => new()
    {
        Visible = false,
        BorderSides = ToolStripStatusLabelBorderSides.Left,
        BorderStyle = Border3DStyle.Etched,
        Padding = new Padding(8, 0, 8, 0),
        ForeColor = Color.FromArgb(45, 65, 90)
    };

    private static Control BuildMetrics(IReadOnlyList<(string Name, string Value)> metrics)
    {
        var table = new TableLayoutPanel { ColumnCount = metrics.Count, RowCount = 1, Height = 94, BackColor = Color.White, Margin = new Padding(0, 0, 0, 14), Padding = Padding.Empty };
        foreach (var _ in metrics) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / metrics.Count));
        for (var i = 0; i < metrics.Count; i++)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(244, 247, 251), Margin = new Padding(i == 0 ? 0 : 5, 0, i == metrics.Count - 1 ? 0 : 5, 0), Padding = new Padding(14, 12, 14, 10) };
            card.Controls.Add(new Label { Text = metrics[i].Value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Color.FromArgb(25, 73, 130), AutoEllipsis = true });
            card.Controls.Add(new Label { Text = metrics[i].Name, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, AutoEllipsis = true });
            table.Controls.Add(card, i, 0);
        }
        return table;
    }

    private static Label SectionHeading(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(45, 60, 80), Margin = new Padding(0, 8, 0, 7) };
    private static Label BodyLabel(string text) => new() { Text = text, AutoSize = false, Height = Math.Max(32, 22 * (text.Count(c => c == '\n') + 1)), Font = new Font("Segoe UI", 10F), Margin = new Padding(0, 0, 0, 12) };

    private void SetDirty()
    {
        _dirty = true;
        if (!Text.EndsWith(" *", StringComparison.Ordinal)) Text += " *";
        _statusLabel.Text = "驗證狀態已修改，尚未儲存。";
    }

    private bool SaveDocument(bool saveAs)
    {
        if (_document is null) return true;
        var path = _documentPath;
        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new SaveFileDialog { Filter = "LogicTrace JSON (*.json)|*.json", FileName = Path.GetFileName(path ?? "logictrace.json") };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try
        {
            _documentService.Save(path!, _document);
            _documentPath = path;
            _documentPathText.Text = Path.GetFullPath(path!);
            _dirty = false;
            Text = $"LogicTrace — {_document.Feature.Name}";
            _statusLabel.Text = $"已儲存 {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"無法儲存檔案：{ex.Message}", "儲存失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_dirty) return true;
        var answer = MessageBox.Show(this, "驗證狀態尚未儲存，是否先儲存？", "LogicTrace", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer switch { DialogResult.Yes => SaveDocument(false), DialogResult.No => true, _ => false };
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }
}
