using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using McTools.Xrm.Connection;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using IOrganizationService = Microsoft.Xrm.Sdk.IOrganizationService;

namespace EnvironmentComparison.Ui
{
    public sealed class EnvironmentComparisonControl : MultipleConnectionsPluginControlBase, IAboutPlugin
    {
        private static readonly Color CanvasColor = Color.FromArgb(245, 247, 250);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(203, 213, 225);
        private static readonly Color TextColor = Color.FromArgb(30, 41, 59);
        private static readonly Color MutedTextColor = Color.FromArgb(100, 116, 139);
        private static readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);
        private static readonly Color CriticalColor = Color.FromArgb(153, 27, 27);
        private static readonly Color CriticalSurface = Color.FromArgb(254, 242, 242);
        private static readonly Color HighColor = Color.FromArgb(154, 52, 18);
        private static readonly Color HighSurface = Color.FromArgb(255, 247, 237);
        private static readonly Color MediumColor = Color.FromArgb(133, 77, 14);
        private static readonly Color MediumSurface = Color.FromArgb(254, 252, 232);

        private readonly DataverseMetadataService _metadataService = new DataverseMetadataService();
        private readonly MetadataComparisonService _comparisonService = new MetadataComparisonService();
        private readonly CsvExportService _csvService = new CsvExportService();
        private readonly HtmlExportService _htmlExportService = new HtmlExportService();
        private readonly RawMetadataExportService _rawMetadataExportService = new RawMetadataExportService();
        private readonly ComparisonOutputFilterService _outputFilterService = new ComparisonOutputFilterService();
        private readonly Panel _viewport = new Panel();
        private readonly TableLayoutPanel _rootLayout = new TableLayoutPanel();
        private readonly TableLayoutPanel _environmentLayout = new TableLayoutPanel();
        private readonly Label _environmentALabel = new Label();
        private readonly Label _environmentBLabel = new Label();
        private readonly Label _summaryLabel = new Label();
        private readonly Label _resultCountLabel = new Label();
        private readonly Button _environmentAButton = new Button();
        private readonly Button _environmentBButton = new Button();
        private readonly Button _compareButton = new Button();
        private readonly Button _exportButton = new Button();
        private readonly Button _htmlExportButton = new Button();
        private readonly Button _rawExportButton = new Button();
        private readonly Button _clearFiltersButton = new Button();
        private readonly CheckBox _tablesCheckBox = new CheckBox();
        private readonly CheckBox _columnsCheckBox = new CheckBox();
        private readonly CheckBox _formsCheckBox = new CheckBox();
        private readonly CheckBox _viewsCheckBox = new CheckBox();
        private readonly CheckBox _reportsCheckBox = new CheckBox();
        private readonly CheckBox _cloudFlowsCheckBox = new CheckBox();
        private readonly CheckBox _businessRulesCheckBox = new CheckBox();
        private readonly CheckBox _workflowsCheckBox = new CheckBox();
        private readonly CheckBox _unpublishedCheckBox = new CheckBox();
        private readonly TextBox _searchBox = new TextBox();
        private readonly Label _tableLogicalNameRegexLabel = new Label();
        private readonly TextBox _tableLogicalNameRegexBox = new TextBox();
        private readonly Label _tableLogicalNameRegexErrorLabel = new Label();
        private readonly ComboBox _scopeFilter = new ComboBox();
        private readonly ComboBox _classificationFilter = new ComboBox();
        private readonly ComboBox _severityFilter = new ComboBox();
        private readonly ComboBox _differenceFilter = new ComboBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly ListView _details = new ListView();
        private readonly ListBox _activity = new ListBox();
        private readonly TabControl _inspectorTabs = new TabControl();
        private readonly ToolTip _toolTip = new ToolTip();
        private Control? _environmentACard;
        private Control? _directionLabel;
        private Control? _environmentBCard;
        private MetadataComparisonResult? _result;
        private bool _resumeComparisonAfterConnectionSelection;
        private bool _busy;
        private bool _compactLayout;
        private bool _updatingClassificationFilter;
        private Regex? _tableLogicalNameRegex;

        private const int CompactThreshold = 760;
        private const int MinimumContentWidth = 350;
        private const int RegexMatchTimeoutMilliseconds = 100;
        private const string AllClassificationsFilterText = "All classifications";
        private const string NoClassificationFilterText = "No classification";
        private const ComparisonAreas TableAssociatedAreas = ComparisonAreas.TableMetadata
            | ComparisonAreas.Columns
            | ComparisonAreas.Forms
            | ComparisonAreas.Views
            | ComparisonAreas.CloudFlows
            | ComparisonAreas.BusinessRules
            | ComparisonAreas.Workflows;

        public EnvironmentComparisonControl()
        {
            InitializeUi();
        }

        internal bool UsesCompactLayout => _compactLayout;

        internal int EnvironmentLayoutColumnCount => _environmentLayout.ColumnCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void UpdateConnection(
            IOrganizationService newService,
            ConnectionDetail detail,
            string actionName = "",
            object? parameter = null)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (actionName == "AdditionalOrganization")
            {
                var selected = AdditionalConnectionDetails.FirstOrDefault(existing => SameSavedConnection(existing, detail));
                if (selected == null)
                {
                    AdditionalConnectionDetails.Add(detail);
                    selected = detail;
                }

                foreach (var existing in AdditionalConnectionDetails.Where(item => item != selected).ToList())
                {
                    RemoveAdditionalOrganization(existing);
                }

                SetConnectionLabel(_environmentBLabel, selected.ConnectionName);
            }
            else
            {
                SetConnectionLabel(_environmentALabel, detail.ConnectionName);
            }

            ClearResult("Connections changed. Compare again to load fresh read-only metadata.");
            ContinuePendingComparison();
        }

        public void ShowAboutDialog()
        {
            var version = GetType().Assembly.GetName().Version?.ToString() ?? "unknown";
            MessageBox.Show(
                this,
                $"Environment Comparison {version}\r\n\r\nRead-only comparison of Dataverse table metadata, columns, forms, system views, organization SSRS reports, cloud flows, business rules, and workflows. Managed/unmanaged status is intentionally ignored.",
                "About Environment Comparison",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        protected override void ConnectionDetailsUpdated(NotifyCollectionChangedEventArgs e)
        {
            if (AdditionalConnectionDetails.Count == 0)
            {
                SetConnectionLabel(_environmentBLabel, "Not selected", false);
                ClearResult("Choose Environment B, then compare metadata.");
            }
        }

        internal enum ComparisonConnectionRequirement
        {
            None,
            EnvironmentA,
            EnvironmentB
        }

        internal static ComparisonConnectionRequirement GetConnectionRequirement(bool hasEnvironmentA, int environmentBCount)
        {
            if (!hasEnvironmentA)
            {
                return ComparisonConnectionRequirement.EnvironmentA;
            }

            return environmentBCount == 1
                ? ComparisonConnectionRequirement.None
                : ComparisonConnectionRequirement.EnvironmentB;
        }

        private void InitializeUi()
        {
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            BackColor = CanvasColor;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _viewport.Dock = DockStyle.Fill;
            _viewport.AutoScroll = true;
            _viewport.BackColor = CanvasColor;

            _rootLayout.Dock = DockStyle.None;
            _rootLayout.ColumnCount = 1;
            _rootLayout.RowCount = 6;
            _rootLayout.Padding = new Padding(14);
            _rootLayout.BackColor = CanvasColor;
            _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            _rootLayout.Controls.Add(CreateHeader(), 0, 0);
            _rootLayout.Controls.Add(CreateConnectionCard(), 0, 1);
            _rootLayout.Controls.Add(CreateOptionsAndActionsCard(), 0, 2);
            _rootLayout.Controls.Add(CreateFilterAndStatusCard(), 0, 3);
            _rootLayout.Controls.Add(CreateGrid(), 0, 4);
            _rootLayout.Controls.Add(CreateInspector(), 0, 5);

            _viewport.Controls.Add(_rootLayout);
            Controls.Add(_viewport);
            SizeChanged += (_, __) => ApplyResponsiveLayout();
            FontChanged += (_, __) => ApplyResponsiveLayout();
            ApplyResponsiveLayout();
        }

        private Control CreateHeader()
        {
            var panel = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 1,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = CanvasColor
            };
            var title = new Label
            {
                AutoSize = true,
                Text = "Environment Comparison",
                Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
                ForeColor = TextColor,
                Margin = new Padding(0, 0, 0, 3)
            };
            var description = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(1100, 0),
                Text = "Choose the areas to compare across two Dataverse environments. The tool reads definitions only and never writes to either environment.",
                ForeColor = MutedTextColor,
                Margin = new Padding(0)
            };
            panel.Controls.Add(title, 0, 0);
            panel.Controls.Add(description, 0, 1);
            return panel;
        }

        private Control CreateConnectionCard()
        {
            var card = CreateCard();
            var layout = (TableLayoutPanel)card;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(SectionLabel("Environments"), 0, 0);

            _environmentLayout.Dock = DockStyle.Top;
            _environmentLayout.AutoSize = true;
            _environmentLayout.Margin = new Padding(0, 7, 0, 8);
            _environmentACard = CreateEnvironmentPanel("Environment A", _environmentALabel, _environmentAButton, "Choose A", RequestEnvironmentA);
            _directionLabel = new Label
            {
                AutoSize = true,
                Text = "compared with",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = MutedTextColor,
                Anchor = AnchorStyles.None,
                Margin = new Padding(12)
            };
            _environmentBCard = CreateEnvironmentPanel("Environment B", _environmentBLabel, _environmentBButton, "Choose B", AddAdditionalOrganization);
            ConfigureEnvironmentLayout(false);
            layout.Controls.Add(_environmentLayout, 0, 1);

            var safety = new Label
            {
                AutoSize = true,
                Text = "READ ONLY  •  No table records  •  No imports, publishes, updates, or deletes  •  Managed/unmanaged status ignored",
                ForeColor = Color.FromArgb(22, 101, 52),
                BackColor = Color.FromArgb(236, 253, 245),
                Padding = new Padding(8, 5, 8, 5),
                Margin = new Padding(0)
            };
            layout.Controls.Add(safety, 0, 2);
            return card;
        }

        private Control CreateOptionsAndActionsCard()
        {
            var card = CreateCard();
            var layout = (TableLayoutPanel)card;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(SectionLabel("Choose what to compare"), 0, 0);

            var areas = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                Margin = new Padding(0, 6, 0, 5)
            };
            ConfigureAreaCheckBox(_tablesCheckBox, "Table metadata", true, "Table-level metadata only; table records are never retrieved.");
            ConfigureAreaCheckBox(_columnsCheckBox, "Columns", true, "Column metadata, types, requirements, security, formats, choices, and other important settings.");
            ConfigureAreaCheckBox(_formsCheckBox, "Forms", true, "System form definitions and important form settings.");
            ConfigureAreaCheckBox(_viewsCheckBox, "System views", true, "System view FetchXML, layout, columns, and important view settings. Personal views are excluded.");
            ConfigureAreaCheckBox(_reportsCheckBox, "SSRS reports", true, "Organization Reporting Services reports, normalized RDL, filters, related tables, categories, and visibility. Personal reports are excluded.");
            ConfigureAreaCheckBox(_cloudFlowsCheckBox, "Cloud flows", true, "Solution-aware modern cloud-flow identity, activation, definition, connection-reference usage, and important settings.");
            ConfigureAreaCheckBox(_businessRulesCheckBox, "Business rules", true, "Business-rule identity, table, scope, activation, and normalized rule definitions.");
            ConfigureAreaCheckBox(_workflowsCheckBox, "Workflows", true, "Classic workflow identity, triggers, execution settings, activation, and normalized workflow definitions.");
            foreach (var checkBox in new[] { _tablesCheckBox, _columnsCheckBox, _formsCheckBox, _viewsCheckBox, _reportsCheckBox, _cloudFlowsCheckBox, _businessRulesCheckBox, _workflowsCheckBox })
            {
                checkBox.CheckedChanged += (_, __) => ComparisonAreasChanged();
            }
            areas.Controls.AddRange(new Control[] { _tablesCheckBox, _columnsCheckBox, _formsCheckBox, _viewsCheckBox, _reportsCheckBox, _cloudFlowsCheckBox, _businessRulesCheckBox, _workflowsCheckBox });
            layout.Controls.Add(areas, 0, 1);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                Margin = new Padding(0, 5, 0, 0)
            };
            _compareButton.Text = "Compare metadata";
            StylePrimaryButton(_compareButton);
            _compareButton.Click += (_, __) => ExecuteMethod(BeginComparison);
            _exportButton.Text = "Export filtered CSV";
            StyleSecondaryButton(_exportButton);
            _exportButton.Enabled = false;
            _exportButton.Click += (_, __) => ExportCsv();
            _htmlExportButton.Text = "Export filterable HTML";
            StyleSecondaryButton(_htmlExportButton);
            _htmlExportButton.Enabled = false;
            _htmlExportButton.Click += (_, __) => ExportHtml();
            _toolTip.SetToolTip(_htmlExportButton, "Browser report containing every difference, with scalable filtering, resizable columns, complete changed values, report RDL, and an automatically loaded CDN diff viewer.");
            _rawExportButton.Text = "Export raw metadata (JSON)";
            StyleSecondaryButton(_rawExportButton);
            _rawExportButton.Enabled = false;
            _rawExportButton.Click += (_, __) => ExportRawMetadata();
            _toolTip.SetToolTip(_rawExportButton, "Temporary structured JSON export of every loaded metadata property from both environments, not only differences.");
            _unpublishedCheckBox.AutoSize = true;
            _unpublishedCheckBox.Text = "Include unpublished metadata";
            _unpublishedCheckBox.Margin = new Padding(14, 8, 8, 4);
            _toolTip.SetToolTip(_unpublishedCheckBox, "Off compares published definitions, which best represents deployed state. Turn on only when draft customizations must be included.");
            actions.Controls.AddRange(new Control[] { _compareButton, _exportButton, _htmlExportButton, _rawExportButton, _unpublishedCheckBox });
            layout.Controls.Add(actions, 0, 2);
            return card;
        }

        private Control CreateFilterAndStatusCard()
        {
            var card = CreateCard();
            var layout = (TableLayoutPanel)card;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _summaryLabel.AutoSize = true;
            _summaryLabel.ForeColor = TextColor;
            _summaryLabel.Text = "Choose two environments and comparison areas, then compare metadata.";
            _summaryLabel.Margin = new Padding(0, 0, 0, 6);
            layout.Controls.Add(_summaryLabel, 0, 0);

            var filters = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                Margin = new Padding(0)
            };
            _searchBox.Width = 245;
            _searchBox.Margin = new Padding(0);
            _searchBox.AccessibleName = "Search comparison issues";
            _searchBox.TextChanged += (_, __) => PopulateGrid();
            _toolTip.SetToolTip(_searchBox, "Search table, component, property, values, and details.");
            var searchFilter = CreateFilterField("Search", _searchBox);
            var tableRegexFilter = CreateTableLogicalNameRegexFilter();
            ConfigureFilter(_scopeFilter, new[] { "All areas", "Tables", "Columns", "Forms", "Views", "Reports", "Cloud flows", "Business rules", "Workflows" });
            ConfigureFilter(_classificationFilter, new[] { AllClassificationsFilterText });
            ConfigureFilter(_severityFilter, new[] { "All severities", "Critical and high", "Critical only" });
            ConfigureFilter(_differenceFilter, new[] { "All differences", "Missing in Environment B", "Missing in Environment A", "Changed" });
            var scopeFilter = CreateFilterField("Area", _scopeFilter);
            var classificationFilter = CreateFilterField("Classification", _classificationFilter);
            var severityFilter = CreateFilterField("Severity", _severityFilter);
            var differenceFilter = CreateFilterField("Difference", _differenceFilter);
            _clearFiltersButton.Text = "Clear filters";
            StyleSecondaryButton(_clearFiltersButton);
            _clearFiltersButton.Click += (_, __) => ClearFilters();
            var clearFilters = CreateFilterField("Actions", _clearFiltersButton);
            filters.Controls.AddRange(new[] { searchFilter, tableRegexFilter, classificationFilter, scopeFilter, severityFilter, differenceFilter, clearFilters });
            layout.Controls.Add(filters, 0, 1);
            UpdateTableLogicalNameRegexAvailability();

            _resultCountLabel.AutoSize = true;
            _resultCountLabel.ForeColor = MutedTextColor;
            _resultCountLabel.Text = "No comparison loaded.";
            _resultCountLabel.Margin = new Padding(0, 6, 0, 0);
            layout.Controls.Add(_resultCountLabel, 0, 2);
            return card;
        }

        private Control CreateGrid()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SurfaceColor,
                Padding = new Padding(1),
                Margin = new Padding(0, 0, 0, 10),
                MinimumSize = new Size(0, 190)
            };
            panel.Paint += (_, args) => ControlPaint.DrawBorder(args.Graphics, panel.ClientRectangle, BorderColor, ButtonBorderStyle.Solid);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToOrderColumns = true;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _grid.BackgroundColor = SurfaceColor;
            _grid.BorderStyle = BorderStyle.None;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.ColumnHeadersHeight = 34;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.EnableHeadersVisualStyles = false;
            _grid.MultiSelect = false;
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.RowTemplate.Height = 29;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            _grid.DefaultCellStyle.SelectionForeColor = TextColor;
            _grid.SelectionChanged += (_, __) => UpdateDetails();
            AddGridColumn("Severity", "Severity", 75);
            AddGridColumn("Scope", "Area", 70);
            AddGridColumn("Difference", "Difference", 145);
            AddGridColumn("Table", "Table", 165);
            AddGridColumn("Classification", "Table classification", 105);
            AddGridColumn("ComponentA", "Component A", 205);
            AddGridColumn("ComponentAId", "Component A (ID)", 175);
            AddGridColumn("ComponentB", "Component B", 205);
            AddGridColumn("ComponentBId", "Component B (ID)", 175);
            AddGridColumn("Property", "Property", 165);
            AddGridColumn("A", "Environment A", 190);
            AddGridColumn("B", "Environment B", 190);
            AddGridColumn("Details", "Details", 370);
            panel.Controls.Add(_grid);
            return panel;
        }

        private Control CreateInspector()
        {
            _inspectorTabs.Dock = DockStyle.Fill;
            _inspectorTabs.Margin = new Padding(0);
            var detailsPage = new TabPage("Selected difference") { BackColor = SurfaceColor, Padding = new Padding(8) };
            _details.Dock = DockStyle.Fill;
            _details.View = View.Details;
            _details.FullRowSelect = true;
            _details.GridLines = true;
            _details.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _details.MultiSelect = false;
            _details.UseCompatibleStateImageBehavior = false;
            _details.Columns.Add("Field", 190);
            _details.Columns.Add("Value", 760);
            detailsPage.Controls.Add(_details);
            var activityPage = new TabPage("Activity") { BackColor = SurfaceColor, Padding = new Padding(8) };
            _activity.Dock = DockStyle.Fill;
            _activity.HorizontalScrollbar = true;
            _activity.IntegralHeight = false;
            _activity.BorderStyle = BorderStyle.None;
            activityPage.Controls.Add(_activity);
            _inspectorTabs.TabPages.Add(detailsPage);
            _inspectorTabs.TabPages.Add(activityPage);
            return _inspectorTabs;
        }

        private void BeginComparison()
        {
            var areas = SelectedAreas;
            if (areas == ComparisonAreas.None)
            {
                MessageBox.Show(this, "Select at least one comparison area.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var requirement = GetConnectionRequirement(Service != null, AdditionalConnectionDetails.Count);
            if (requirement != ComparisonConnectionRequirement.None)
            {
                _resumeComparisonAfterConnectionSelection = true;
                PromptForConnection(requirement);
                return;
            }

            _resumeComparisonAfterConnectionSelection = false;
            var serviceA = Service;
            var detailB = AdditionalConnectionDetails[0];
            var serviceB = detailB.GetCrmServiceClient();
            if (serviceA == null || serviceB == null)
            {
                MessageBox.Show(this, "Both connections must be available.", "Connections required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (ConnectionDetail != null && SameEnvironment(ConnectionDetail, detailB))
            {
                MessageBox.Show(this, "Environment A and Environment B are the same saved environment. Choose two different environments.", "Connections must differ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var includeUnpublished = _unpublishedCheckBox.Checked;
            SetBusy(true);
            _activity.Items.Insert(0, $"{DateTime.Now:T} Loading {DisplayAreas(areas)} from both environments...");
            WorkAsync(new WorkAsyncInfo(
                "Loading read-only Dataverse definitions...",
                (worker, eventArgs) =>
                {
                    var snapshotA = _metadataService.LoadSnapshot(
                        serviceA,
                        areas,
                        includeUnpublished,
                        (percent, message) => worker.ReportProgress(percent / 2, $"Environment A: {message}"));
                    var snapshotB = _metadataService.LoadSnapshot(
                        serviceB,
                        areas,
                        includeUnpublished,
                        (percent, message) => worker.ReportProgress(50 + percent / 2, $"Environment B: {message}"));
                    eventArgs.Result = new ComparisonLoadResult(_comparisonService.Compare(snapshotA, snapshotB), includeUnpublished);
                })
            {
                ProgressChanged = eventArgs => SetWorkingMessage(eventArgs.UserState?.ToString() ?? "Loading..."),
                PostWorkCallBack = ComparisonCompleted
            });
        }

        private void ComparisonCompleted(RunWorkerCompletedEventArgs eventArgs)
        {
            SetBusy(false);
            if (eventArgs.Error != null)
            {
                LogError(eventArgs.Error.ToString());
                _activity.Items.Insert(0, $"{DateTime.Now:T} Comparison failed: {eventArgs.Error.Message}");
                MessageBox.Show(this, $"The comparison could not be loaded.\r\n\r\n{eventArgs.Error.Message}", "Comparison failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var loaded = (ComparisonLoadResult)eventArgs.Result;
            _result = loaded.Result;
            PopulateClassificationFilter();
            PopulateGrid();
            var mode = loaded.IncludedUnpublished ? "published and unpublished" : "published";
            _summaryLabel.Text = $"Compared {DisplayAreas(_result.EnvironmentA.IncludedAreas)} using {mode} definitions. No changes were made.";
            _activity.Items.Insert(0, $"{DateTime.Now:T} Comparison completed with {_result.Issues.Count:N0} differences. No changes were made.");
            _exportButton.Enabled = _grid.Rows.Count > 0;
            _htmlExportButton.Enabled = _result.Issues.Count > 0;
            _rawExportButton.Enabled = true;
        }

        private void PopulateGrid()
        {
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                if (_result == null)
                {
                    _resultCountLabel.Text = "No comparison loaded.";
                    _exportButton.Enabled = false;
                    _htmlExportButton.Enabled = false;
                    _rawExportButton.Enabled = false;
                    UpdateDetails();
                    return;
                }

                var regexValid = TryFilterIssuesByTableLogicalName(_result.Issues, out var regexScopedIssues);
                var filtered = regexScopedIssues.Where(MatchesFilters).ToList();
                foreach (var issue in filtered)
                {
                    var rowIndex = _grid.Rows.Add(
                        issue.Severity,
                        DisplayScope(issue.Scope),
                        DisplayDifference(issue.Kind),
                        DisplayTable(issue),
                        issue.TableClassification,
                        issue.EnvironmentAComponent,
                        issue.EnvironmentAComponentId,
                        issue.EnvironmentBComponent,
                        issue.EnvironmentBComponentId,
                        issue.PropertyName,
                        issue.EnvironmentAPreviewValue,
                        issue.EnvironmentBPreviewValue,
                        issue.Details);
                    var row = _grid.Rows[rowIndex];
                    row.Tag = issue;
                    StyleIssueRow(row, issue.Severity);
                }

                var missingB = filtered.Count(issue => issue.Kind == DifferenceKind.MissingInEnvironmentB);
                var missingA = filtered.Count(issue => issue.Kind == DifferenceKind.MissingInEnvironmentA);
                var changed = filtered.Count(issue => issue.Kind == DifferenceKind.Changed);
                _resultCountLabel.Text = $"Showing {filtered.Count:N0} of {regexScopedIssues.Count:N0} differences  •  Missing in B: {missingB:N0}  •  Missing in A: {missingA:N0}  •  Changed: {changed:N0}";
                _exportButton.Enabled = !_busy && regexValid && filtered.Count > 0;
                _htmlExportButton.Enabled = !_busy && regexValid && regexScopedIssues.Count > 0;
                _rawExportButton.Enabled = !_busy && regexValid;
                if (_grid.Rows.Count > 0)
                {
                    _grid.Rows[0].Selected = true;
                    _grid.CurrentCell = _grid.Rows[0].Cells[0];
                }
                UpdateDetails();
            }
            finally
            {
                _grid.ResumeLayout();
            }
        }

        private bool MatchesFilters(ComparisonIssue issue)
        {
            if (_scopeFilter.SelectedIndex > 0 && (int)issue.Scope != _scopeFilter.SelectedIndex - 1) return false;
            if (_classificationFilter.SelectedIndex > 0)
            {
                var selectedClassification = _classificationFilter.SelectedItem?.ToString() ?? string.Empty;
                if (string.Equals(selectedClassification, NoClassificationFilterText, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(issue.TableClassification)) return false;
                }
                else if (!string.Equals(issue.TableClassification, selectedClassification, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            if (_severityFilter.SelectedIndex == 1 && issue.Severity > DifferenceSeverity.High) return false;
            if (_severityFilter.SelectedIndex == 2 && issue.Severity != DifferenceSeverity.Critical) return false;
            if (_differenceFilter.SelectedIndex > 0)
            {
                var selectedKind = _differenceFilter.SelectedIndex == 1
                    ? DifferenceKind.MissingInEnvironmentB
                    : _differenceFilter.SelectedIndex == 2
                        ? DifferenceKind.MissingInEnvironmentA
                        : DifferenceKind.Changed;
                if (issue.Kind != selectedKind) return false;
            }

            var search = _searchBox.Text.Trim();
            if (search.Length == 0) return true;
            return new[]
                {
                    issue.TableLogicalName,
                    issue.TableDisplayName,
                    issue.TableClassification,
                    issue.ComponentKey,
                    issue.ComponentName,
                    issue.EnvironmentAComponent,
                    issue.EnvironmentAComponentId,
                    issue.EnvironmentBComponent,
                    issue.EnvironmentBComponentId,
                    issue.PropertyName,
                    issue.EnvironmentAPreviewValue,
                    issue.EnvironmentBPreviewValue,
                    issue.Details
                }
                .Any(value => value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void UpdateDetails()
        {
            _details.BeginUpdate();
            try
            {
                _details.Items.Clear();
                var issue = _grid.CurrentRow?.Tag as ComparisonIssue;
                if (issue == null)
                {
                    AddDetail("Selection", "Select a difference row to inspect it.");
                    return;
                }

                AddDetail("Severity", issue.Severity.ToString());
                AddDetail("Area", DisplayScope(issue.Scope));
                AddDetail("Difference", DisplayDifference(issue.Kind));
                AddDetail("Table", DisplayTable(issue));
                AddDetail("Table classification", issue.TableClassification);
                AddDetail("Component A", issue.EnvironmentAComponent);
                AddDetail("Component A (ID)", issue.EnvironmentAComponentId);
                AddDetail("Component B", issue.EnvironmentBComponent);
                AddDetail("Component B (ID)", issue.EnvironmentBComponentId);
                AddDetail("Property", issue.PropertyName);
                AddDetail("Environment A", issue.EnvironmentAPreviewValue);
                AddDetail("Environment B", issue.EnvironmentBPreviewValue);
                AddDetail("Details", issue.Details);
            }
            finally
            {
                _details.EndUpdate();
            }
        }

        private void ExportCsv()
        {
            var issues = _grid.Rows.Cast<DataGridViewRow>()
                .Select(row => row.Tag as ComparisonIssue)
                .Where(issue => issue != null)
                .Cast<ComparisonIssue>()
                .ToList();
            if (issues.Count == 0)
            {
                MessageBox.Show(this, "There are no filtered differences to export.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"Dataverse-environment-comparison-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                OverwritePrompt = true,
                Title = "Export filtered metadata differences"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, _csvService.Create(issues), new UTF8Encoding(true));
                _activity.Items.Insert(0, $"{DateTime.Now:T} Exported {issues.Count:N0} filtered differences to {dialog.FileName}.");
                MessageBox.Show(this, $"Exported {issues.Count:N0} differences.", "CSV exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportHtml()
        {
            var unfilteredResult = _result;
            if (unfilteredResult == null || unfilteredResult.Issues.Count == 0)
            {
                MessageBox.Show(this, "Run a comparison with at least one difference before exporting HTML.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryFilterResultByTableLogicalName(unfilteredResult, out var result)) return;
            if (result.Issues.Count == 0)
            {
                MessageBox.Show(this, "No differences match the table logical-name regular expression.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var environmentAName = _environmentALabel.Text.Trim();
            var environmentBName = _environmentBLabel.Text.Trim();

            using (var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "html",
                Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*",
                FileName = $"Dataverse-environment-comparison-{DateTime.Now:yyyyMMdd-HHmmss}.html",
                OverwritePrompt = true,
                Title = "Export complete filterable comparison"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var fileName = dialog.FileName;
                SetBusy(true);
                _activity.Items.Insert(0, $"{DateTime.Now:T} Exporting complete filterable HTML comparison...");
                WorkAsync(new WorkAsyncInfo(
                    "Creating the filterable HTML report...",
                    (_, eventArgs) =>
                    {
                        using (var writer = new StreamWriter(fileName, false, new UTF8Encoding(false)))
                        {
                            eventArgs.Result = _htmlExportService.Write(writer, result, environmentAName, environmentBName);
                        }
                    })
                {
                    PostWorkCallBack = eventArgs => HtmlExportCompleted(eventArgs, fileName)
                });
            }
        }

        private void HtmlExportCompleted(RunWorkerCompletedEventArgs eventArgs, string fileName)
        {
            SetBusy(false);
            if (eventArgs.Error != null)
            {
                LogError(eventArgs.Error.ToString());
                _activity.Items.Insert(0, $"{DateTime.Now:T} HTML export failed: {eventArgs.Error.Message}");
                MessageBox.Show(this, $"The HTML export failed.\r\n\r\n{eventArgs.Error.Message}", "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var issueCount = (int)eventArgs.Result;
            _activity.Items.Insert(0, $"{DateTime.Now:T} Exported {issueCount:N0} differences to the filterable HTML report at {fileName}.");
            MessageBox.Show(this, $"Exported {issueCount:N0} differences as a filterable HTML report. Its enhanced diff viewer loads automatically when internet access is available; the base report works without it.", "HTML exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportRawMetadata()
        {
            var unfilteredResult = _result;
            if (unfilteredResult == null)
            {
                MessageBox.Show(this, "Run a comparison before exporting raw metadata.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryFilterResultByTableLogicalName(unfilteredResult, out var result)) return;

            using (var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "json",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"Dataverse-environment-raw-metadata-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                OverwritePrompt = true,
                Title = "Export raw metadata for diagnostics"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var fileName = dialog.FileName;
                SetBusy(true);
                _activity.Items.Insert(0, $"{DateTime.Now:T} Exporting complete raw metadata snapshot...");
                WorkAsync(new WorkAsyncInfo(
                    "Exporting raw metadata without changing either environment...",
                    (_, eventArgs) =>
                    {
                        using (var writer = new StreamWriter(fileName, false, new UTF8Encoding(true)))
                        {
                            eventArgs.Result = _rawMetadataExportService.Write(writer, result);
                        }
                    })
                {
                    PostWorkCallBack = eventArgs => RawMetadataExportCompleted(eventArgs, fileName)
                });
            }
        }

        private void RawMetadataExportCompleted(RunWorkerCompletedEventArgs eventArgs, string fileName)
        {
            SetBusy(false);
            if (eventArgs.Error != null)
            {
                LogError(eventArgs.Error.ToString());
                _activity.Items.Insert(0, $"{DateTime.Now:T} Raw metadata export failed: {eventArgs.Error.Message}");
                MessageBox.Show(this, $"The raw metadata export failed.\r\n\r\n{eventArgs.Error.Message}", "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var propertyCount = (int)eventArgs.Result;
            _activity.Items.Insert(0, $"{DateTime.Now:T} Exported {propertyCount:N0} raw metadata properties to {fileName}.");
            MessageBox.Show(this, $"Exported {propertyCount:N0} raw metadata properties as structured JSON.", "Raw metadata exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private ComparisonAreas SelectedAreas
        {
            get
            {
                var areas = ComparisonAreas.None;
                if (_tablesCheckBox.Checked) areas |= ComparisonAreas.TableMetadata;
                if (_columnsCheckBox.Checked) areas |= ComparisonAreas.Columns;
                if (_formsCheckBox.Checked) areas |= ComparisonAreas.Forms;
                if (_viewsCheckBox.Checked) areas |= ComparisonAreas.Views;
                if (_reportsCheckBox.Checked) areas |= ComparisonAreas.Reports;
                if (_cloudFlowsCheckBox.Checked) areas |= ComparisonAreas.CloudFlows;
                if (_businessRulesCheckBox.Checked) areas |= ComparisonAreas.BusinessRules;
                if (_workflowsCheckBox.Checked) areas |= ComparisonAreas.Workflows;
                return areas;
            }
        }

        private void RequestEnvironmentA()
        {
            RaiseRequestConnectionEvent(new RequestConnectionEventArgs { ActionName = string.Empty, Control = this });
        }

        private void PromptForConnection(ComparisonConnectionRequirement requirement)
        {
            if (requirement == ComparisonConnectionRequirement.EnvironmentA)
            {
                _summaryLabel.Text = AdditionalConnectionDetails.Count == 1
                    ? "Choose Environment A to continue."
                    : "Choose Environment A first; the Environment B selector will open next.";
                RequestEnvironmentA();
            }
            else
            {
                _summaryLabel.Text = "Choose Environment B to continue.";
                AddAdditionalOrganization();
            }
        }

        private void ContinuePendingComparison()
        {
            if (!_resumeComparisonAfterConnectionSelection || IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new MethodInvoker(() =>
            {
                if (!_resumeComparisonAfterConnectionSelection || IsDisposed) return;
                var requirement = GetConnectionRequirement(Service != null, AdditionalConnectionDetails.Count);
                if (requirement != ComparisonConnectionRequirement.None)
                {
                    PromptForConnection(requirement);
                    return;
                }

                _resumeComparisonAfterConnectionSelection = false;
                ExecuteMethod(BeginComparison);
            }));
        }

        private void ClearResult(string message)
        {
            _result = null;
            PopulateClassificationFilter();
            _grid.Rows.Clear();
            _details.Items.Clear();
            _summaryLabel.Text = message;
            _resultCountLabel.Text = "No comparison loaded.";
            _exportButton.Enabled = false;
            _htmlExportButton.Enabled = false;
            _rawExportButton.Enabled = false;
        }

        private void ClearFilters()
        {
            _searchBox.Clear();
            _tableLogicalNameRegexBox.Clear();
            _scopeFilter.SelectedIndex = 0;
            _classificationFilter.SelectedIndex = 0;
            _severityFilter.SelectedIndex = 0;
            _differenceFilter.SelectedIndex = 0;
            PopulateGrid();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _compareButton.Enabled = !busy;
            _environmentAButton.Enabled = !busy;
            _environmentBButton.Enabled = !busy;
            _tablesCheckBox.Enabled = !busy;
            _columnsCheckBox.Enabled = !busy;
            _formsCheckBox.Enabled = !busy;
            _viewsCheckBox.Enabled = !busy;
            _reportsCheckBox.Enabled = !busy;
            _cloudFlowsCheckBox.Enabled = !busy;
            _businessRulesCheckBox.Enabled = !busy;
            _workflowsCheckBox.Enabled = !busy;
            _unpublishedCheckBox.Enabled = !busy;
            UpdateTableLogicalNameRegexAvailability();
            var regexValid = !TableLogicalNameRegexHasError;
            _exportButton.Enabled = !busy && regexValid && _grid.Rows.Count > 0;
            _htmlExportButton.Enabled = !busy && regexValid && _result != null && _result.Issues.Count > 0;
            _rawExportButton.Enabled = !busy && regexValid && _result != null;
            UseWaitCursor = busy;
        }

        private Control CreateTableLogicalNameRegexFilter()
        {
            var panel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 8, 0)
            };
            _tableLogicalNameRegexLabel.AutoSize = true;
            _tableLogicalNameRegexLabel.Text = "Table logical name regex";
            _tableLogicalNameRegexLabel.ForeColor = MutedTextColor;
            _tableLogicalNameRegexLabel.Margin = new Padding(0, 0, 0, 2);
            _tableLogicalNameRegexBox.Width = 220;
            _tableLogicalNameRegexBox.Margin = new Padding(0);
            _tableLogicalNameRegexBox.AccessibleName = "Table logical name regular expression";
            _tableLogicalNameRegexBox.TextChanged += (_, __) => TableLogicalNameRegexChanged();
            _toolTip.SetToolTip(
                _tableLogicalNameRegexBox,
                "Filters table-associated preview and exports after retrieval. Example: ^(ata_|mshied_). Reports and other organization-level rows are retained.");
            _tableLogicalNameRegexErrorLabel.AutoSize = true;
            _tableLogicalNameRegexErrorLabel.ForeColor = CriticalColor;
            _tableLogicalNameRegexErrorLabel.Margin = new Padding(0, 2, 0, 0);
            panel.Controls.Add(_tableLogicalNameRegexLabel, 0, 0);
            panel.Controls.Add(_tableLogicalNameRegexBox, 0, 1);
            panel.Controls.Add(_tableLogicalNameRegexErrorLabel, 0, 2);
            return panel;
        }

        private static Control CreateFilterField(string caption, Control control)
        {
            var panel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 8, 0)
            };
            var label = new Label
            {
                AutoSize = true,
                Text = caption,
                ForeColor = MutedTextColor,
                Margin = new Padding(0, 0, 0, 2)
            };
            control.Margin = new Padding(0);
            panel.Controls.Add(label, 0, 0);
            panel.Controls.Add(control, 0, 1);
            return panel;
        }

        private void ComparisonAreasChanged()
        {
            UpdateTableLogicalNameRegexAvailability();
            PopulateGrid();
        }

        private void TableLogicalNameRegexChanged()
        {
            CompileTableLogicalNameRegex();
            PopulateGrid();
        }

        private void UpdateTableLogicalNameRegexAvailability()
        {
            var appliesToSelectedAreas = TableLogicalNameRegexAppliesToSelectedAreas;
            _tableLogicalNameRegexLabel.Enabled = appliesToSelectedAreas;
            _tableLogicalNameRegexBox.Enabled = !_busy && appliesToSelectedAreas;
            CompileTableLogicalNameRegex();
        }

        private void CompileTableLogicalNameRegex()
        {
            _tableLogicalNameRegex = null;
            SetTableLogicalNameRegexError(string.Empty);
            var pattern = _tableLogicalNameRegexBox.Text.Trim();
            if (!TableLogicalNameRegexAppliesToSelectedAreas || pattern.Length == 0) return;

            try
            {
                _tableLogicalNameRegex = new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(RegexMatchTimeoutMilliseconds));
            }
            catch (ArgumentException)
            {
                SetTableLogicalNameRegexError("Invalid regular expression.");
            }
        }

        private bool TryFilterIssuesByTableLogicalName(
            IEnumerable<ComparisonIssue> issues,
            out IReadOnlyList<ComparisonIssue> filteredIssues)
        {
            if (!TryGetActiveTableLogicalNameRegex(out var regex))
            {
                filteredIssues = issues.ToList();
                return false;
            }

            if (regex == null)
            {
                filteredIssues = issues.ToList();
                return true;
            }

            try
            {
                filteredIssues = _outputFilterService.FilterIssuesByTableLogicalName(issues, regex);
                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                SetTableLogicalNameRegexError("Regular expression took too long.");
                filteredIssues = issues.ToList();
                return false;
            }
        }

        private bool TryFilterResultByTableLogicalName(
            MetadataComparisonResult result,
            out MetadataComparisonResult filteredResult)
        {
            if (!TryGetActiveTableLogicalNameRegex(out var regex))
            {
                MessageBox.Show(this, _tableLogicalNameRegexErrorLabel.Text, "Invalid table filter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                filteredResult = result;
                return false;
            }

            if (regex == null)
            {
                filteredResult = result;
                return true;
            }

            try
            {
                filteredResult = _outputFilterService.FilterByTableLogicalName(result, regex);
                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                SetTableLogicalNameRegexError("Regular expression took too long.");
                MessageBox.Show(this, _tableLogicalNameRegexErrorLabel.Text, "Invalid table filter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                filteredResult = result;
                return false;
            }
        }

        private bool TryGetActiveTableLogicalNameRegex(out Regex? regex)
        {
            regex = null;
            if (!TableLogicalNameRegexAppliesToSelectedAreas || _tableLogicalNameRegexBox.Text.Trim().Length == 0) return true;
            if (_tableLogicalNameRegex == null || TableLogicalNameRegexHasError) return false;
            regex = _tableLogicalNameRegex;
            return true;
        }

        private void SetTableLogicalNameRegexError(string message)
        {
            _tableLogicalNameRegexErrorLabel.Text = message;
            _tableLogicalNameRegexErrorLabel.Visible = message.Length > 0;
        }

        private bool TableLogicalNameRegexAppliesToSelectedAreas => (SelectedAreas & TableAssociatedAreas) != 0;

        private bool TableLogicalNameRegexHasError => _tableLogicalNameRegexErrorLabel.Text.Length > 0;

        private void PopulateClassificationFilter()
        {
            var previousSelection = _classificationFilter.SelectedItem?.ToString() ?? AllClassificationsFilterText;
            var classifications = (_result?.Issues ?? Array.Empty<ComparisonIssue>())
                .Select(issue => issue.TableClassification.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var hasUnclassified = _result?.Issues.Any(issue => string.IsNullOrWhiteSpace(issue.TableClassification)) == true;

            _updatingClassificationFilter = true;
            _classificationFilter.BeginUpdate();
            try
            {
                _classificationFilter.Items.Clear();
                _classificationFilter.Items.Add(AllClassificationsFilterText);
                foreach (var classification in classifications) _classificationFilter.Items.Add(classification);
                if (hasUnclassified) _classificationFilter.Items.Add(NoClassificationFilterText);
                var previousIndex = _classificationFilter.Items.IndexOf(previousSelection);
                _classificationFilter.SelectedIndex = previousIndex >= 0 ? previousIndex : 0;
            }
            finally
            {
                _classificationFilter.EndUpdate();
                _updatingClassificationFilter = false;
            }
        }

        private void ApplyResponsiveLayout()
        {
            if (_viewport.ClientSize.Width <= 0 || _viewport.ClientSize.Height <= 0) return;
            var compact = _viewport.ClientSize.Width < CompactThreshold;
            if (compact != _compactLayout || _environmentLayout.ColumnCount == 0)
            {
                _compactLayout = compact;
                ConfigureEnvironmentLayout(compact);
            }

            var contentWidth = Math.Max(MinimumContentWidth, _viewport.ClientSize.Width - 2);
            var minimumHeight = compact ? 820 : 690;
            _rootLayout.Size = new Size(contentWidth, Math.Max(_viewport.ClientSize.Height, minimumHeight));
            _rootLayout.PerformLayout();
        }

        private void ConfigureEnvironmentLayout(bool compact)
        {
            if (_environmentACard == null || _environmentBCard == null || _directionLabel == null) return;
            _environmentLayout.SuspendLayout();
            _environmentLayout.Controls.Clear();
            _environmentLayout.ColumnStyles.Clear();
            _environmentLayout.RowStyles.Clear();
            if (compact)
            {
                _environmentLayout.ColumnCount = 1;
                _environmentLayout.RowCount = 3;
                _environmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _environmentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _environmentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _environmentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _environmentLayout.Controls.Add(_environmentACard, 0, 0);
                _environmentLayout.Controls.Add(_directionLabel, 0, 1);
                _environmentLayout.Controls.Add(_environmentBCard, 0, 2);
            }
            else
            {
                _environmentLayout.ColumnCount = 3;
                _environmentLayout.RowCount = 1;
                _environmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
                _environmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _environmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
                _environmentLayout.Controls.Add(_environmentACard, 0, 0);
                _environmentLayout.Controls.Add(_directionLabel, 1, 0);
                _environmentLayout.Controls.Add(_environmentBCard, 2, 0);
            }
            _environmentLayout.ResumeLayout(true);
        }

        private static TableLayoutPanel CreateCard()
        {
            var card = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ColumnCount = 1,
                BackColor = SurfaceColor,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            card.Paint += (_, args) => ControlPaint.DrawBorder(args.Graphics, card.ClientRectangle, BorderColor, ButtonBorderStyle.Solid);
            return card;
        }

        private static Label SectionLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextColor,
                Margin = new Padding(0)
            };
        }

        private Control CreateEnvironmentPanel(string caption, Label valueLabel, Button button, string buttonText, Action action)
        {
            var panel = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10),
                Margin = new Padding(0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var captionLabel = new Label
            {
                AutoSize = true,
                Text = caption,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = TextColor,
                Margin = new Padding(0, 0, 0, 3)
            };
            valueLabel.AutoSize = true;
            valueLabel.Text = "Not selected";
            valueLabel.ForeColor = MutedTextColor;
            valueLabel.AutoEllipsis = true;
            valueLabel.Margin = new Padding(0);
            button.Text = buttonText;
            StyleSecondaryButton(button);
            button.Click += (_, __) => action();
            panel.Controls.Add(captionLabel, 0, 0);
            panel.SetColumnSpan(captionLabel, 2);
            panel.Controls.Add(valueLabel, 0, 1);
            panel.Controls.Add(button, 1, 1);
            return panel;
        }

        private void ConfigureAreaCheckBox(CheckBox checkBox, string text, bool isChecked, string tooltip)
        {
            checkBox.AutoSize = true;
            checkBox.Text = text;
            checkBox.Checked = isChecked;
            checkBox.Margin = new Padding(0, 4, 18, 4);
            _toolTip.SetToolTip(checkBox, tooltip);
        }

        private void ConfigureFilter(ComboBox comboBox, IEnumerable<string> items)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Width = 170;
            comboBox.Margin = new Padding(0);
            comboBox.Items.AddRange(items.Cast<object>().ToArray());
            comboBox.SelectedIndex = 0;
            comboBox.SelectedIndexChanged += (_, __) =>
            {
                if (!_updatingClassificationFilter) PopulateGrid();
            };
        }

        private void AddGridColumn(string name, string header, int width)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.Automatic
            });
        }

        private void AddDetail(string field, string value)
        {
            var item = new ListViewItem(field);
            item.SubItems.Add(value ?? string.Empty);
            _details.Items.Add(item);
        }

        private static void StyleIssueRow(DataGridViewRow row, DifferenceSeverity severity)
        {
            switch (severity)
            {
                case DifferenceSeverity.Critical:
                    row.DefaultCellStyle.BackColor = CriticalSurface;
                    row.Cells[0].Style.ForeColor = CriticalColor;
                    break;
                case DifferenceSeverity.High:
                    row.DefaultCellStyle.BackColor = HighSurface;
                    row.Cells[0].Style.ForeColor = HighColor;
                    break;
                case DifferenceSeverity.Medium:
                    row.DefaultCellStyle.BackColor = MediumSurface;
                    row.Cells[0].Style.ForeColor = MediumColor;
                    break;
            }
        }

        private static void StylePrimaryButton(Button button)
        {
            button.AutoSize = true;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = PrimaryColor;
            button.ForeColor = Color.White;
            button.Padding = new Padding(9, 4, 9, 4);
            button.Margin = new Padding(0, 3, 8, 3);
            button.Cursor = Cursors.Hand;
        }

        private static void StyleSecondaryButton(Button button)
        {
            button.AutoSize = true;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = BorderColor;
            button.BackColor = SurfaceColor;
            button.ForeColor = TextColor;
            button.Padding = new Padding(8, 3, 8, 3);
            button.Margin = new Padding(0, 3, 8, 3);
            button.Cursor = Cursors.Hand;
        }

        private static void SetConnectionLabel(Label label, string? connectionName, bool selected = true)
        {
            label.Text = string.IsNullOrWhiteSpace(connectionName) ? "Not selected" : connectionName;
            label.ForeColor = selected ? TextColor : MutedTextColor;
        }

        private static string DisplayTable(ComparisonIssue issue)
        {
            return string.IsNullOrWhiteSpace(issue.TableDisplayName)
                ? issue.TableLogicalName
                : $"{issue.TableDisplayName} ({issue.TableLogicalName})";
        }

        private static string DisplayDifference(DifferenceKind kind)
        {
            switch (kind)
            {
                case DifferenceKind.MissingInEnvironmentA:
                    return "Missing in A";
                case DifferenceKind.MissingInEnvironmentB:
                    return "Missing in B";
                default:
                    return "Changed";
            }
        }

        private static string DisplayAreas(ComparisonAreas areas)
        {
            var labels = new List<string>();
            if ((areas & ComparisonAreas.TableMetadata) != 0) labels.Add("table metadata");
            if ((areas & ComparisonAreas.Columns) != 0) labels.Add("columns");
            if ((areas & ComparisonAreas.Forms) != 0) labels.Add("forms");
            if ((areas & ComparisonAreas.Views) != 0) labels.Add("system views");
            if ((areas & ComparisonAreas.Reports) != 0) labels.Add("SSRS reports");
            if ((areas & ComparisonAreas.CloudFlows) != 0) labels.Add("cloud flows");
            if ((areas & ComparisonAreas.BusinessRules) != 0) labels.Add("business rules");
            if ((areas & ComparisonAreas.Workflows) != 0) labels.Add("workflows");
            return string.Join(", ", labels);
        }

        private static string DisplayScope(ComparisonScope scope)
        {
            switch (scope)
            {
                case ComparisonScope.CloudFlow:
                    return "Cloud Flow";
                case ComparisonScope.BusinessRule:
                    return "Business Rule";
                default:
                    return scope.ToString();
            }
        }

        private static bool SameSavedConnection(ConnectionDetail first, ConnectionDetail second)
        {
            if (ReferenceEquals(first, second)) return true;
            return first.ConnectionId.HasValue
                && second.ConnectionId.HasValue
                && first.ConnectionId.Value == second.ConnectionId.Value;
        }

        private static bool SameEnvironment(ConnectionDetail first, ConnectionDetail second)
        {
            if (SameSavedConnection(first, second)) return true;
            if (!string.IsNullOrWhiteSpace(first.EnvironmentId)
                && !string.IsNullOrWhiteSpace(second.EnvironmentId)
                && string.Equals(first.EnvironmentId.Trim(), second.EnvironmentId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var firstUrl = NormalizeEnvironmentUrl(first.OrganizationDataServiceUrl ?? first.OrganizationServiceUrl);
            var secondUrl = NormalizeEnvironmentUrl(second.OrganizationDataServiceUrl ?? second.OrganizationServiceUrl);
            return firstUrl.Length > 0 && string.Equals(firstUrl, secondUrl, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeEnvironmentUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                ? parsed.GetLeftPart(UriPartial.Authority).TrimEnd('/')
                : url!.Trim().TrimEnd('/');
        }
    }
}
