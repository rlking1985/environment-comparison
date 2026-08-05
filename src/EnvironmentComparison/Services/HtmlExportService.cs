using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Services
{
    public sealed class HtmlExportService
    {
        public int Write(
            TextWriter writer,
            IEnumerable<ComparisonIssue> issues,
            string environmentAName = "Environment A",
            string environmentBName = "Environment B")
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (issues == null) throw new ArgumentNullException(nameof(issues));

            var rows = issues.ToList();
            return WriteDocument(writer, rows, Array.Empty<ReportDefinitionInfo>(), environmentAName, environmentBName);
        }

        public int Write(
            TextWriter writer,
            MetadataComparisonResult result,
            string environmentAName = "Environment A",
            string environmentBName = "Environment B")
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var rows = result.Issues.ToList();
            var reportsA = result.EnvironmentA.Reports.ToDictionary(report => report.Key, StringComparer.OrdinalIgnoreCase);
            var reportsB = result.EnvironmentB.Reports.ToDictionary(report => report.Key, StringComparer.OrdinalIgnoreCase);
            var definitions = rows
                .Where(issue => issue.Scope == ComparisonScope.Report)
                .GroupBy(issue => issue.ComponentKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var issue = group.First();
                    reportsA.TryGetValue(issue.EnvironmentAComponentKey, out var reportA);
                    reportsB.TryGetValue(issue.EnvironmentBComponentKey, out var reportB);
                    return new ReportDefinitionInfo(
                        issue.ComponentKey,
                        reportA?.Name ?? reportB?.Name ?? string.Empty,
                        reportA?.GetProperty("RDL") ?? string.Empty,
                        reportB?.GetProperty("RDL") ?? string.Empty,
                        reportA?.GetProperty("Raw RDL") ?? string.Empty,
                        reportB?.GetProperty("Raw RDL") ?? string.Empty);
                })
                .ToList();
            return WriteDocument(writer, rows, definitions, environmentAName, environmentBName);
        }

        private static int WriteDocument(
            TextWriter writer,
            IReadOnlyList<ComparisonIssue> rows,
            IReadOnlyList<ReportDefinitionInfo> reportDefinitions,
            string environmentAName,
            string environmentBName)
        {
            writer.Write(DocumentStart);
            WriteData(
                writer,
                rows,
                reportDefinitions,
                NormalizeEnvironmentName(environmentAName, "Environment A"),
                NormalizeEnvironmentName(environmentBName, "Environment B"));
            writer.Write(DocumentEnd);
            return rows.Count;
        }

        private static void WriteData(
            TextWriter writer,
            IReadOnlyList<ComparisonIssue> issues,
            IReadOnlyList<ReportDefinitionInfo> reportDefinitions,
            string environmentAName,
            string environmentBName)
        {
            writer.Write('{');
            WriteString(writer, "environmentAName", environmentAName, true);
            WriteString(writer, "environmentBName", environmentBName, true);
            writer.Write("\"formatVersion\":1,\"rowCount\":");
            writer.Write(issues.Count.ToString(CultureInfo.InvariantCulture));
            writer.Write(",\"rows\":[");
            for (var index = 0; index < issues.Count; index++)
            {
                if (index > 0) writer.Write(',');
                WriteIssue(writer, issues[index], index + 1);
            }

            writer.Write("],\"reportDefinitions\":[");
            for (var index = 0; index < reportDefinitions.Count; index++)
            {
                if (index > 0) writer.Write(',');
                WriteReportDefinition(writer, reportDefinitions[index]);
            }

            writer.Write("]}");
        }

        private static string NormalizeEnvironmentName(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
        }

        private static void WriteReportDefinition(TextWriter writer, ReportDefinitionInfo definition)
        {
            writer.Write('{');
            WriteString(writer, "key", definition.Key, true);
            WriteString(writer, "name", definition.Name, true);
            WriteString(writer, "aRdl", definition.EnvironmentARdl, true);
            WriteString(writer, "bRdl", definition.EnvironmentBRdl, true);
            WriteString(writer, "aRawRdl", definition.EnvironmentARawRdl, true);
            WriteString(writer, "bRawRdl", definition.EnvironmentBRawRdl, false);
            writer.Write('}');
        }

        private static void WriteIssue(TextWriter writer, ComparisonIssue issue, int id)
        {
            writer.Write('{');
            WriteNumber(writer, "id", id, true);
            WriteString(writer, "severity", issue.Severity.ToString(), true);
            WriteNumber(writer, "severityOrder", (int)issue.Severity, true);
            WriteString(writer, "scope", issue.Scope.ToString(), true);
            WriteString(writer, "scopeDisplay", DisplayScope(issue.Scope), true);
            WriteString(writer, "difference", DisplayKind(issue.Kind), true);
            WriteBoolean(writer, "inspectable", issue.Kind == DifferenceKind.Changed, true);
            WriteString(writer, "tableLogical", issue.TableLogicalName, true);
            WriteString(writer, "tableDisplay", issue.TableDisplayName, true);
            WriteString(writer, "classification", issue.TableClassification, true);
            WriteString(writer, "componentKey", issue.ComponentKey, true);
            WriteString(writer, "componentName", issue.ComponentName, true);
            WriteString(writer, "componentA", issue.EnvironmentAComponent, true);
            WriteString(writer, "componentAId", issue.EnvironmentAComponentId, true);
            WriteString(writer, "componentB", issue.EnvironmentBComponent, true);
            WriteString(writer, "componentBId", issue.EnvironmentBComponentId, true);
            WriteString(writer, "property", issue.PropertyName, true);
            WriteString(writer, "a", issue.EnvironmentAValue, true);
            WriteString(writer, "b", issue.EnvironmentBValue, true);
            WriteString(writer, "aPreview", issue.EnvironmentAPreviewValue, true);
            WriteString(writer, "bPreview", issue.EnvironmentBPreviewValue, true);
            WriteString(writer, "details", issue.Details, false);
            writer.Write('}');
        }

        private static void WriteNumber(TextWriter writer, string name, int value, bool trailingComma)
        {
            WriteJsonString(writer, name);
            writer.Write(':');
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
            if (trailingComma) writer.Write(',');
        }

        private static void WriteBoolean(TextWriter writer, string name, bool value, bool trailingComma)
        {
            WriteJsonString(writer, name);
            writer.Write(':');
            writer.Write(value ? "true" : "false");
            if (trailingComma) writer.Write(',');
        }

        private static void WriteString(TextWriter writer, string name, string value, bool trailingComma)
        {
            WriteJsonString(writer, name);
            writer.Write(':');
            WriteJsonString(writer, value);
            if (trailingComma) writer.Write(',');
        }

        private static void WriteJsonString(TextWriter writer, string? value)
        {
            writer.Write('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': writer.Write("\\\""); break;
                    case '\\': writer.Write("\\\\"); break;
                    case '\b': writer.Write("\\b"); break;
                    case '\f': writer.Write("\\f"); break;
                    case '\n': writer.Write("\\n"); break;
                    case '\r': writer.Write("\\r"); break;
                    case '\t': writer.Write("\\t"); break;
                    case '<': writer.Write("\\u003C"); break;
                    case '>': writer.Write("\\u003E"); break;
                    case '&': writer.Write("\\u0026"); break;
                    case '\u2028': writer.Write("\\u2028"); break;
                    case '\u2029': writer.Write("\\u2029"); break;
                    default:
                        if (character < 0x20)
                        {
                            writer.Write("\\u");
                            writer.Write(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            writer.Write(character);
                        }

                        break;
                }
            }

            writer.Write('"');
        }

        private static string DisplayKind(DifferenceKind kind)
        {
            switch (kind)
            {
                case DifferenceKind.MissingInEnvironmentA:
                    return "Missing in Environment A";
                case DifferenceKind.MissingInEnvironmentB:
                    return "Missing in Environment B";
                default:
                    return "Changed";
            }
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

        private sealed class ReportDefinitionInfo
        {
            public ReportDefinitionInfo(
                string key,
                string name,
                string environmentARdl,
                string environmentBRdl,
                string environmentARawRdl,
                string environmentBRawRdl)
            {
                Key = key;
                Name = name;
                EnvironmentARdl = environmentARdl;
                EnvironmentBRdl = environmentBRdl;
                EnvironmentARawRdl = environmentARawRdl;
                EnvironmentBRawRdl = environmentBRawRdl;
            }

            public string Key { get; }

            public string Name { get; }

            public string EnvironmentARdl { get; }

            public string EnvironmentBRdl { get; }

            public string EnvironmentARawRdl { get; }

            public string EnvironmentBRawRdl { get; }
        }

        private const string DocumentStart = @"<!doctype html>
<html lang='en'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width,initial-scale=1'>
  <title>Dataverse environment comparison</title>
  <style>
    :root { color-scheme: light; --canvas:#f4f7fb; --surface:#fff; --border:#d7dfeb; --text:#172033; --muted:#667085; --primary:#2563eb; --primary-soft:#eff6ff; --critical:#991b1b; --critical-bg:#fef2f2; --high:#9a3412; --high-bg:#fff7ed; --medium:#854d0e; --medium-bg:#fefce8; --low:#475569; --low-bg:#f8fafc; --shadow:0 16px 40px rgba(15,23,42,.12); }
    * { box-sizing:border-box; }
    html,body { height:100%; }
    body { margin:0; overflow:hidden; background:var(--canvas); color:var(--text); font:14px/1.45 Segoe UI,Arial,sans-serif; }
    button,input,select { font:inherit; }
    button { cursor:pointer; }
    .shell { width:100%; height:100%; margin:0; padding:20px clamp(14px,1.25vw,32px); display:flex; flex-direction:column; }
    .hero { flex:0 0 auto; display:flex; align-items:flex-start; justify-content:space-between; gap:24px; margin-bottom:14px; }
    h1 { margin:0 0 4px; font-size:26px; letter-spacing:-.02em; }
    .subtle { color:var(--muted); }
    .offline { white-space:nowrap; color:#166534; background:#ecfdf3; border:1px solid #bbf7d0; border-radius:999px; padding:6px 10px; font-weight:600; }
    .summary { flex:0 0 auto; display:grid; grid-template-columns:repeat(5,minmax(130px,1fr)); gap:10px; margin-bottom:12px; }
    .summary-card { background:var(--surface); border:1px solid var(--border); border-radius:10px; padding:12px 14px; box-shadow:0 2px 8px rgba(15,23,42,.04); }
    .summary-card span { display:block; color:var(--muted); font-size:12px; text-transform:uppercase; letter-spacing:.04em; }
    .summary-card strong { display:block; margin-top:3px; font-size:22px; }
    .panel { min-height:0; flex:1 1 auto; display:flex; flex-direction:column; overflow:hidden; background:var(--surface); border:1px solid var(--border); border-radius:12px; box-shadow:0 2px 10px rgba(15,23,42,.05); }
    .filters { flex:0 0 auto; display:grid; grid-template-columns:minmax(260px,2fr) repeat(6,minmax(130px,1fr)) auto; gap:10px; padding:12px 14px; align-items:start; }
    .filters > .button { margin-top:20px; }
    .field label { display:block; margin-bottom:4px; color:var(--muted); font-size:12px; font-weight:600; }
    .field input,.field select { width:100%; min-height:36px; border:1px solid var(--border); border-radius:7px; background:#fff; color:var(--text); padding:6px 9px; }
    .field input:focus,.field select:focus { outline:2px solid #bfdbfe; border-color:var(--primary); }
    .search-options { display:flex; align-items:center; gap:8px; margin-top:6px; color:var(--muted); font-size:12px; }
    .search-options input { width:auto; min-height:auto; }
    .button { min-height:36px; border:1px solid var(--border); border-radius:7px; padding:6px 12px; background:#fff; color:var(--text); font-weight:600; }
    .button:hover { background:#f8fafc; }
    .button.primary { color:#fff; background:var(--primary); border-color:var(--primary); }
    .button:disabled { opacity:.45; cursor:not-allowed; }
    .button[hidden] { display:none; }
    .statusbar { flex:0 0 auto; display:flex; flex-wrap:wrap; justify-content:space-between; gap:8px 16px; align-items:center; padding:8px 14px; border-top:1px solid var(--border); color:var(--muted); }
    .filter-progress { height:3px; background:#e2e8f0; overflow:hidden; display:none; }
    .filter-progress.active { display:block; }
    .filter-progress::after { content:''; display:block; width:30%; height:100%; background:var(--primary); animation:progress 1s ease-in-out infinite; }
    @keyframes progress { from { transform:translateX(-100%); } to { transform:translateX(430%); } }
    .table-wrap { min-height:0; flex:1 1 auto; overflow:auto; overscroll-behavior:contain; scrollbar-gutter:stable both-edges; border-top:1px solid var(--border); }
    #resultsTable { width:1765px; min-width:1765px; table-layout:fixed; border-collapse:separate; border-spacing:0; }
    #resultsTable > thead th { position:sticky; top:0; z-index:2; padding:0; overflow:visible; background:#f8fafc; border-bottom:1px solid var(--border); text-align:left; white-space:nowrap; }
    #resultsTable > thead th button { width:100%; padding:10px 9px; border:0; background:transparent; color:var(--text); text-align:left; font-weight:700; }
    #resultsTable > thead th button:hover { background:#eef2f7; }
    .ec-column-resizer { position:absolute; top:0; right:0; bottom:0; z-index:6; width:12px; cursor:col-resize; touch-action:none; outline:none; }
    .ec-column-resizer::after { content:''; position:absolute; top:18%; bottom:18%; right:0; width:2px; background:#cbd5e1; }
    .ec-column-resizer:hover::after,.ec-column-resizer:focus::after,.ec-column-resizer.active::after { width:3px; background:var(--primary); }
    body.resizing-columns { cursor:col-resize; user-select:none; }
    th[aria-sort='ascending'] button::after { content:'  ▲'; color:var(--primary); }
    th[aria-sort='descending'] button::after { content:'  ▼'; color:var(--primary); }
    #resultsTable > tbody td { padding:9px; border-bottom:1px solid #edf1f6; vertical-align:top; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    #resultsTable > tbody tr:hover { background:#f8fbff; }
    #resultsTable > tbody tr.selected { background:var(--primary-soft); }
    #resultsTable > thead th.action-column,#resultsTable > tbody td.action-column { position:sticky; right:0; border-left:1px solid var(--border); box-shadow:-5px 0 8px rgba(15,23,42,.05); }
    #resultsTable > thead th.action-column { z-index:5; background:#f8fafc; }
    #resultsTable > tbody td.action-column { z-index:1; background:#fff; text-align:center; }
    #resultsTable > tbody tr:hover td.action-column { background:#f8fbff; }
    #resultsTable > tbody tr.selected td.action-column { background:var(--primary-soft); }
    .not-inspectable { color:#94a3b8; }
    .badge { display:inline-flex; border-radius:999px; padding:2px 8px; font-size:12px; font-weight:700; }
    .badge.Critical { color:var(--critical); background:var(--critical-bg); }
    .badge.High { color:var(--high); background:var(--high-bg); }
    .badge.Medium { color:var(--medium); background:var(--medium-bg); }
    .badge.Low { color:var(--low); background:var(--low-bg); }
    .inspect { border:1px solid #bfdbfe; border-radius:6px; background:var(--primary-soft); color:#1d4ed8; padding:4px 8px; font-weight:600; }
    .empty { padding:38px; text-align:center; color:var(--muted); }
    .pager { flex:0 0 auto; display:flex; align-items:center; justify-content:space-between; gap:12px; padding:10px 14px; border-top:1px solid var(--border); }
    .pager-controls { display:flex; align-items:center; gap:6px; }
    .pager-controls .button { min-width:38px; padding:5px 9px; }
    .page-size { display:flex; align-items:center; gap:7px; color:var(--muted); }
    .page-size select { min-height:32px; border:1px solid var(--border); border-radius:6px; background:#fff; }
    .backdrop { position:fixed; inset:0; z-index:20; background:rgba(15,23,42,.45); }
    .drawer { position:fixed; inset:16px; z-index:21; min-width:0; min-height:0; display:flex; flex-direction:column; background:var(--surface); border:1px solid var(--border); border-radius:14px; box-shadow:var(--shadow); overflow:hidden; }
    .drawer[hidden],.backdrop[hidden] { display:none; }
    .drawer-head { flex:0 0 auto; display:flex; align-items:flex-start; justify-content:space-between; gap:18px; padding:12px 16px; border-bottom:1px solid var(--border); }
    .drawer-head h2 { margin:0 0 3px; font-size:19px; }
    .drawer-actions { display:flex; flex-wrap:wrap; gap:7px; }
    .drawer-meta { flex:0 0 auto; display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:1px; background:var(--border); border-bottom:1px solid var(--border); }
    .meta-item { min-width:0; padding:9px 12px; background:#f8fafc; }
    .meta-item span { display:block; color:var(--muted); font-size:11px; text-transform:uppercase; }
    .meta-item strong { display:block; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .ec-diff-toolbar { position:relative; z-index:6; flex:0 0 auto !important; display:flex !important; min-height:54px; visibility:visible !important; flex-wrap:wrap; align-items:end; gap:8px 10px; padding:8px 12px; overflow:visible; border-bottom:1px solid var(--border); background:#fff; }
    .ec-diff-toolbar .field { min-width:180px; }
    .ec-diff-toolbar .field.source { min-width:250px; }
    .cdn-status { min-width:150px; color:var(--muted); font-size:12px; }
    .definition-grid { min-height:0; flex:1 1 auto; display:grid; grid-template-columns:minmax(0,1fr) 10px minmax(0,1fr); gap:0; overflow:hidden; background:var(--border); }
    .definition-grid[hidden] { display:none; }
    .definition { min-width:0; min-height:0; display:flex; flex-direction:column; background:#fff; }
    .ec-pane-resizer { position:relative; z-index:2; min-width:10px; cursor:col-resize; touch-action:none; outline:none; background:#e2e8f0; }
    .ec-pane-resizer::after { content:''; position:absolute; top:0; bottom:0; left:4px; width:2px; background:#94a3b8; }
    .ec-pane-resizer:hover::after,.ec-pane-resizer:focus::after,.ec-pane-resizer.active::after { width:3px; background:var(--primary); }
    .definition-head { flex:0 0 auto; display:flex; flex-wrap:wrap; justify-content:space-between; align-items:center; gap:7px 10px; padding:8px 10px; background:#f8fafc; border-bottom:1px solid var(--border); font-weight:700; }
    pre { flex:1; min-height:0; margin:0; padding:12px; overflow:auto; overscroll-behavior:contain; scrollbar-gutter:stable; white-space:pre-wrap; overflow-wrap:anywhere; tab-size:2; font:12px/1.45 Consolas,Cascadia Mono,monospace; color:#0f172a; }
    .nowrap pre { white-space:pre; overflow-wrap:normal; }
    .diff-panel { min-width:0; min-height:0; flex:1 1 auto; overflow:scroll; overscroll-behavior:contain; scrollbar-gutter:stable both-edges; padding:12px; background:#fff; }
    .diff-panel[hidden] { display:none; }
    .diff-placeholder { padding:28px; border:1px dashed var(--border); border-radius:8px; color:var(--muted); text-align:center; }
    #diffOutput { min-width:100%; }
    #diffOutput .d2h-wrapper { min-width:100%; text-align:left; }
    #diffOutput .d2h-file-wrapper { min-width:100%; margin-bottom:0; }
    #diffOutput .d2h-file-side-diff { overflow:visible; }
    #diffOutput .d2h-code-line-ctn { display:block; white-space:pre-wrap; overflow-wrap:anywhere; word-break:break-word; }
    .nowrap #diffOutput .d2h-code-line-ctn { display:inline-block; }
    .nowrap #diffOutput .d2h-code-line-ctn { white-space:pre; overflow-wrap:normal; word-break:normal; }
    .drawer-foot { flex:0 0 auto; padding:8px 12px; border-top:1px solid var(--border); background:#f8fafc; color:var(--muted); }
    .toast { position:fixed; right:24px; bottom:24px; z-index:30; padding:10px 14px; border-radius:8px; color:#fff; background:#172033; box-shadow:var(--shadow); }
    .toast[hidden] { display:none; }
    @media (max-width:1100px) { .summary { grid-template-columns:repeat(3,1fr); } .filters { grid-template-columns:repeat(3,1fr); } .filters .search { grid-column:1/-1; } .drawer-meta { grid-template-columns:repeat(2,1fr); } }
    @media (max-width:720px) { .shell { padding:10px; } .hero { display:block; } .offline { display:inline-block; margin-top:8px; } .summary { grid-template-columns:repeat(2,1fr); } .filters { grid-template-columns:1fr; max-height:32vh; overflow:auto; } .filters .search { grid-column:auto; } .statusbar,.pager { align-items:flex-start; flex-direction:column; } .drawer { inset:0; border-radius:0; } .definition-grid { grid-template-columns:1fr !important; grid-template-rows:minmax(0,1fr) minmax(0,1fr); } .ec-pane-resizer { display:none; } .ec-diff-toolbar { align-items:stretch; max-height:28vh; overflow:auto; } .ec-diff-toolbar .field { min-width:100%; } }
    @media (max-height:650px) { .hero { display:none; } .summary { display:none; } .shell { padding:8px; } .filters { max-height:30vh; overflow:auto; } .drawer-meta { display:none; } .ec-diff-toolbar { max-height:32vh; overflow:auto; } }
    @media print { html,body,.shell { height:auto; overflow:visible; } .panel { display:block; overflow:visible; } .filters,.pager,.offline,.inspect,.drawer,.backdrop { display:none !important; } .table-wrap { max-height:none; overflow:visible; } th,th.action-column,td.action-column { position:static; box-shadow:none; } }
  </style>
</head>
<body>
  <main class='shell'>
    <header class='hero'>
      <div><h1>Dataverse environment comparison</h1><div class='subtle'>Environment A: <strong id='environmentAName'>Environment A</strong> (reference) &nbsp;&rarr;&nbsp; Environment B: <strong id='environmentBName'>Environment B</strong> (comparison target)</div></div>
      <div class='offline'>Base report works offline · no environment access</div>
    </header>
    <section class='summary' aria-label='Comparison summary'>
      <div class='summary-card'><span>Total differences</span><strong id='totalCount'>0</strong></div>
      <div class='summary-card'><span>Filtered results</span><strong id='filteredCount'>0</strong></div>
      <div class='summary-card'><span>Critical</span><strong id='criticalCount'>0</strong></div>
      <div class='summary-card'><span>High</span><strong id='highCount'>0</strong></div>
      <div class='summary-card'><span>Missing in B</span><strong id='missingBCount'>0</strong></div>
    </section>
    <section class='panel' aria-label='Comparison results'>
      <div class='filters'>
        <div class='field search'><label for='search'>Search results</label><input id='search' type='search' placeholder='Table, component, property, preview or details'><label class='search-options'><input id='searchFullValues' type='checkbox'> Also search complete A/B values (slower)</label></div>
        <div class='field'><label for='severityFilter'>Severity</label><select id='severityFilter'><option value=''>All severities</option><option>Critical</option><option>High</option><option>Medium</option><option>Low</option></select></div>
        <div class='field'><label for='scopeFilter'>Area</label><select id='scopeFilter'><option value=''>All areas</option><option>Table</option><option>Column</option><option>Form</option><option>View</option><option>Report</option><option value='CloudFlow'>Cloud Flow</option><option value='BusinessRule'>Business Rule</option><option>Workflow</option></select></div>
        <div class='field'><label for='differenceFilter'>Difference</label><select id='differenceFilter'><option value=''>All differences</option><option>Missing in Environment B</option><option>Missing in Environment A</option><option>Changed</option></select></div>
        <div class='field'><label for='tableFilter'>Table</label><select id='tableFilter'><option value=''>All tables</option></select></div>
        <div class='field'><label for='classificationFilter'>Classification</label><select id='classificationFilter'><option value=''>All classifications</option></select></div>
        <div class='field'><label for='propertyFilter'>Property</label><select id='propertyFilter'><option value=''>All properties</option></select></div>
        <button id='clearFilters' class='button' type='button'>Clear filters</button>
      </div>
      <div id='filterProgress' class='filter-progress' aria-hidden='true'></div>
      <div class='statusbar'><span id='statusText' aria-live='polite'>Loading comparison data…</span><span><button id='resetColumns' class='button' type='button'>Reset column widths</button> &nbsp; Click <strong>Inspect</strong> to view or diff complete values.</span></div>
      <div class='table-wrap'>
        <table id='resultsTable'>
          <colgroup><col style='width:105px'><col style='width:100px'><col style='width:190px'><col style='width:240px'><col style='width:120px'><col style='width:240px'><col style='width:180px'><col style='width:240px'><col style='width:180px'><col style='width:170px'><col style='width:250px'><col style='width:250px'><col style='width:90px'></colgroup>
          <thead><tr>
            <th data-key='severityOrder' aria-sort='ascending'><button type='button' data-sort='severityOrder'>Severity</button><span class='ec-column-resizer' data-column-index='0' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Severity column'></span></th>
            <th data-key='scope'><button type='button' data-sort='scope'>Area</button><span class='ec-column-resizer' data-column-index='1' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Area column'></span></th>
            <th data-key='difference'><button type='button' data-sort='difference'>Difference</button><span class='ec-column-resizer' data-column-index='2' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Difference column'></span></th>
            <th data-key='tableLogical'><button type='button' data-sort='tableLogical'>Table</button><span class='ec-column-resizer' data-column-index='3' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Table column'></span></th>
            <th data-key='classification'><button type='button' data-sort='classification'>Classification</button><span class='ec-column-resizer' data-column-index='4' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Classification column'></span></th>
            <th data-key='componentA'><button type='button' data-sort='componentA'>Component A</button><span class='ec-column-resizer' data-column-index='5' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Component A column'></span></th>
            <th data-key='componentAId'><button type='button' data-sort='componentAId'>Component A (ID)</button><span class='ec-column-resizer' data-column-index='6' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Component A ID column'></span></th>
            <th data-key='componentB'><button type='button' data-sort='componentB'>Component B</button><span class='ec-column-resizer' data-column-index='7' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Component B column'></span></th>
            <th data-key='componentBId'><button type='button' data-sort='componentBId'>Component B (ID)</button><span class='ec-column-resizer' data-column-index='8' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Component B ID column'></span></th>
            <th data-key='property'><button type='button' data-sort='property'>Property</button><span class='ec-column-resizer' data-column-index='9' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Property column'></span></th>
            <th data-key='aPreview'><button type='button' data-sort='aPreview'>Environment A</button><span class='ec-column-resizer' data-column-index='10' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Environment A column'></span></th>
            <th data-key='bPreview'><button type='button' data-sort='bPreview'>Environment B</button><span class='ec-column-resizer' data-column-index='11' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Environment B column'></span></th>
            <th class='action-column'><span style='display:block;padding:10px 9px'>Inspect</span><span class='ec-column-resizer' data-column-index='12' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Inspect column'></span></th>
          </tr></thead>
          <tbody id='resultsBody'></tbody>
        </table>
      </div>
      <div class='pager'>
        <div class='page-size'><label for='pageSize'>Rows per page</label><select id='pageSize'><option>50</option><option selected>100</option><option>250</option><option>500</option></select></div>
        <span id='pageStatus'>Page 1 of 1</span>
        <div class='pager-controls'><button id='firstPage' class='button' type='button' title='First page'>«</button><button id='previousPage' class='button' type='button' title='Previous page'>‹</button><button id='nextPage' class='button' type='button' title='Next page'>›</button><button id='lastPage' class='button' type='button' title='Last page'>»</button></div>
      </div>
    </section>
  </main>
  <div id='backdrop' class='backdrop' hidden></div>
  <section id='drawer' class='drawer' role='dialog' aria-modal='true' aria-labelledby='detailHeading' hidden>
    <div class='drawer-head'><div><h2 id='detailHeading'>Selected difference</h2><div id='detailDescription' class='subtle'></div></div><div class='drawer-actions'><button id='showDiff' class='button primary' type='button' disabled>Loading enhanced diff…</button><button id='wrapToggle' class='button' type='button'>Disable wrapping</button><button id='closeDrawer' class='button primary' type='button'>Close</button></div></div>
    <div id='drawerMeta' class='drawer-meta'></div>
    <div class='ec-diff-toolbar' role='toolbar' aria-label='Value and diff controls'>
      <div class='field source'><label for='valueSource'>Values to inspect</label><select id='valueSource'><option value='property'>Compared property values</option></select></div>
      <div class='field'><label for='diffFormat'>Diff layout</label><select id='diffFormat' disabled><option value='side-by-side'>Side by side</option><option value='line-by-line'>Line by line</option></select></div>
      <button id='loadDiffLibraries' class='button' type='button' hidden>Retry CDN diff viewer</button>
      <span id='cdnStatus' class='cdn-status'>Loading enhanced diff…</span>
    </div>
    <div id='definitionGrid' class='definition-grid'>
      <section class='definition'><div class='definition-head'><span>Environment A · <span id='lengthA'>0 characters</span></span><span><button id='copyA' class='button' type='button'>Copy</button> <button id='downloadA' class='button' type='button'>Download</button></span></div><pre id='valueA'></pre></section>
      <div id='valuePaneResizer' class='ec-pane-resizer' role='separator' tabindex='0' aria-orientation='vertical' aria-label='Resize Environment A and Environment B panes' aria-valuemin='15' aria-valuemax='85' aria-valuenow='50'></div>
      <section class='definition'><div class='definition-head'><span>Environment B · <span id='lengthB'>0 characters</span></span><span><button id='copyB' class='button' type='button'>Copy</button> <button id='downloadB' class='button' type='button'>Download</button></span></div><pre id='valueB'></pre></section>
    </div>
    <div id='diffPanel' class='diff-panel' hidden><div id='diffOutput'><div class='diff-placeholder'>Load the CDN diff viewer, then select Show enhanced diff.</div></div></div>
    <div class='drawer-foot'>Values are complete comparison values; large XML is not truncated or split into Excel-sized parts. Report rows also provide the full normalized and raw RDL XML.</div>
  </section>
  <div id='toast' class='toast' role='status' hidden></div>
  <script id='comparison-data' type='application/json'>";

        private const string DocumentEnd = @"</script>
  <script>
    'use strict';
    const dataElement = document.getElementById('comparison-data');
    const report = JSON.parse(dataElement.textContent);
    dataElement.remove();
    const allRows = report.rows;
    const reportDefinitions = new Map((report.reportDefinitions || []).map(definition => [String(definition.key || '').toLocaleLowerCase(), definition]));
    report.reportDefinitions = null;
    const elements = Object.fromEntries(Array.from(document.querySelectorAll('[id]')).map(element => [element.id, element]));
    const environmentAName = String(report.environmentAName || 'Environment A');
    const environmentBName = String(report.environmentBName || 'Environment B');
    elements.environmentAName.textContent = environmentAName;
    elements.environmentBName.textContent = environmentBName;
    document.title = `${environmentAName} vs ${environmentBName} - Dataverse environment comparison`;
    const jsDiffUrl = 'https://cdn.jsdelivr.net/npm/diff@9.0.0/dist/diff.min.js';
    const diff2HtmlUrl = 'https://cdn.jsdelivr.net/npm/diff2html@3.4.56/bundles/js/diff2html.min.js';
    const diff2HtmlCssUrl = 'https://cdn.jsdelivr.net/npm/diff2html@3.4.56/bundles/css/diff2html.min.css';
    const initialColumnWidths = [105,100,190,240,120,240,180,240,180,170,250,250,90];
    let filteredRows = allRows.slice();
    let currentPage = 1;
    let pageSize = 100;
    let sortKey = 'severityOrder';
    let sortDirection = 1;
    let filterGeneration = 0;
    let filterTimer = 0;
    let selectedRow = null;
    let enhancedDiffState = 'not-loaded';
    let diffGeneration = 0;
    let valuePaneRatio = 0.5;

    function formatNumber(value) { return Number(value || 0).toLocaleString(); }
    function displayTable(row) { return row.tableDisplay ? `${row.tableDisplay} (${row.tableLogical})` : row.tableLogical; }
    function normalize(value) { return String(value || '').toLocaleLowerCase(); }

    function createOption(value, label) {
      const option = document.createElement('option');
      option.value = value;
      option.textContent = label;
      return option;
    }

    function addOptions(select, values, emptyLabel) {
      const fragment = document.createDocumentFragment();
      values.forEach(value => {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = value === '__none__' ? emptyLabel : value;
        fragment.appendChild(option);
      });
      select.appendChild(fragment);
    }

    function initializeFilters() {
      const tables = Array.from(new Set(allRows.map(row => row.tableLogical || '__none__'))).sort((a,b) => a.localeCompare(b, undefined, {sensitivity:'base'}));
      const classifications = Array.from(new Set(allRows.map(row => row.classification || '__none__'))).sort((a,b) => a.localeCompare(b, undefined, {sensitivity:'base'}));
      const properties = Array.from(new Set(allRows.map(row => row.property).filter(Boolean))).sort((a,b) => a.localeCompare(b, undefined, {sensitivity:'base'}));
      addOptions(elements.tableFilter, tables, 'No associated table');
      addOptions(elements.classificationFilter, classifications, 'No classification');
      addOptions(elements.propertyFilter, properties, 'No property');
    }

    function setColumnWidth(index, width) {
      const columns = elements.resultsTable.querySelectorAll('col');
      if (!columns[index]) return;
      const nextWidth = Math.max(70, Math.round(width));
      columns[index].style.width = `${nextWidth}px`;
      const total = Array.from(columns).reduce((sum, column) => sum + (parseFloat(column.style.width) || 70), 0);
      const tableWidth = Math.max(total, elements.resultsTable.parentElement.clientWidth);
      elements.resultsTable.style.width = `${tableWidth}px`;
      elements.resultsTable.style.minWidth = `${tableWidth}px`;
      const handle = elements.resultsTable.querySelector(`.ec-column-resizer[data-column-index='${index}']`);
      if (handle) handle.setAttribute('aria-valuenow', String(nextWidth));
    }

    function resetColumnWidths() {
      const availableWidth = elements.resultsTable.parentElement.clientWidth;
      const initialTotal = initialColumnWidths.reduce((sum, width) => sum + width, 0);
      const scale = Math.max(1, availableWidth / initialTotal);
      initialColumnWidths.forEach((width, index) => setColumnWidth(index, width * scale));
    }

    function fitResultsTableToContainer() {
      const columns = Array.from(elements.resultsTable.querySelectorAll('col'));
      const currentTotal = columns.reduce((sum, column) => sum + (parseFloat(column.style.width) || 70), 0);
      const availableWidth = elements.resultsTable.parentElement.clientWidth;
      if (!availableWidth || currentTotal >= availableWidth) return;
      const scale = availableWidth / currentTotal;
      columns.forEach((column, index) => setColumnWidth(index, (parseFloat(column.style.width) || 70) * scale));
    }

    function initializeColumnResizing() {
      elements.resultsTable.querySelectorAll('.ec-column-resizer').forEach(handle => {
        const index = Number(handle.dataset.columnIndex);
        if (!Number.isInteger(index) || !initialColumnWidths[index]) return;
        handle.addEventListener('pointerdown', event => {
          if (event.pointerType === 'mouse' && event.button !== 0) return;
          event.preventDefault();
          event.stopPropagation();
          const startX = event.clientX;
          const column = elements.resultsTable.querySelectorAll('col')[index];
          const startWidth = parseFloat(column.style.width) || column.getBoundingClientRect().width;
          handle.classList.add('active');
          document.body.classList.add('resizing-columns');
          handle.setPointerCapture(event.pointerId);

          const move = moveEvent => {
            moveEvent.preventDefault();
            setColumnWidth(index, startWidth + moveEvent.clientX - startX);
          };
          const stop = stopEvent => {
            handle.classList.remove('active');
            document.body.classList.remove('resizing-columns');
            handle.removeEventListener('pointermove', move);
            handle.removeEventListener('pointerup', stop);
            handle.removeEventListener('pointercancel', stop);
            if (handle.hasPointerCapture(stopEvent.pointerId)) handle.releasePointerCapture(stopEvent.pointerId);
          };
          handle.addEventListener('pointermove', move, {passive:false});
          handle.addEventListener('pointerup', stop, {once:true});
          handle.addEventListener('pointercancel', stop, {once:true});
        });
        handle.addEventListener('dblclick', event => {
          event.preventDefault();
          event.stopPropagation();
          const initialTotal = initialColumnWidths.reduce((sum, width) => sum + width, 0);
          const scale = Math.max(1, elements.resultsTable.parentElement.clientWidth / initialTotal);
          setColumnWidth(index, initialColumnWidths[index] * scale);
        });
        handle.addEventListener('keydown', event => {
          if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
          event.preventDefault();
          event.stopPropagation();
          const column = elements.resultsTable.querySelectorAll('col')[index];
          const currentWidth = parseFloat(column.style.width) || initialColumnWidths[index];
          setColumnWidth(index, currentWidth + (event.key === 'ArrowRight' ? 10 : -10));
        });
      });
      resetColumnWidths();
      let tableResizeFrame = 0;
      window.addEventListener('resize', () => {
        window.cancelAnimationFrame(tableResizeFrame);
        tableResizeFrame = window.requestAnimationFrame(fitResultsTableToContainer);
      });
    }

    function applyValuePaneSplit() {
      if (window.matchMedia('(max-width:720px)').matches) {
        elements.definitionGrid.style.gridTemplateColumns = '';
        return;
      }
      const width = elements.definitionGrid.clientWidth;
      if (!width) return;
      const dividerWidth = elements.valuePaneResizer.offsetWidth || 10;
      const available = Math.max(0, width - dividerWidth);
      const leftWidth = Math.round(available * valuePaneRatio);
      elements.definitionGrid.style.gridTemplateColumns = `${leftWidth}px ${dividerWidth}px ${available - leftWidth}px`;
      elements.valuePaneResizer.setAttribute('aria-valuenow', String(Math.round(valuePaneRatio * 100)));
    }

    function initializeValuePaneResizing() {
      const handle = elements.valuePaneResizer;
      handle.addEventListener('pointerdown', event => {
        if (event.pointerType === 'mouse' && event.button !== 0) return;
        event.preventDefault();
        const bounds = elements.definitionGrid.getBoundingClientRect();
        const dividerWidth = handle.offsetWidth || 10;
        const available = Math.max(1, bounds.width - dividerWidth);
        handle.classList.add('active');
        document.body.classList.add('resizing-columns');
        handle.setPointerCapture(event.pointerId);

        const move = moveEvent => {
          moveEvent.preventDefault();
          valuePaneRatio = Math.max(0.15, Math.min(0.85, (moveEvent.clientX - bounds.left - dividerWidth / 2) / available));
          applyValuePaneSplit();
        };
        const stop = stopEvent => {
          handle.classList.remove('active');
          document.body.classList.remove('resizing-columns');
          handle.removeEventListener('pointermove', move);
          handle.removeEventListener('pointerup', stop);
          handle.removeEventListener('pointercancel', stop);
          if (handle.hasPointerCapture(stopEvent.pointerId)) handle.releasePointerCapture(stopEvent.pointerId);
        };
        handle.addEventListener('pointermove', move, {passive:false});
        handle.addEventListener('pointerup', stop, {once:true});
        handle.addEventListener('pointercancel', stop, {once:true});
      });
      handle.addEventListener('dblclick', () => {
        valuePaneRatio = 0.5;
        applyValuePaneSplit();
      });
      handle.addEventListener('keydown', event => {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        event.preventDefault();
        valuePaneRatio = Math.max(0.15, Math.min(0.85, valuePaneRatio + (event.key === 'ArrowRight' ? 0.05 : -0.05)));
        applyValuePaneSplit();
      });
      window.addEventListener('resize', () => {
        if (!elements.drawer.hidden) applyValuePaneSplit();
      });
    }

    function rowSearchText(row) {
      if (!row._searchText) {
        row._searchText = normalize([row.severity,row.scopeDisplay,row.difference,row.tableLogical,row.tableDisplay,row.classification,row.componentA,row.componentAId,row.componentB,row.componentBId,row.componentKey,row.componentName,row.property,row.aPreview,row.bPreview,row.details].join('\n'));
      }
      return row._searchText;
    }

    function matchesSearch(row, terms, includeFullValues) {
      if (!terms.length) return true;
      const summary = rowSearchText(row);
      let a = null;
      let b = null;
      return terms.every(term => {
        if (summary.includes(term)) return true;
        if (!includeFullValues) return false;
        if (a === null) a = normalize(row.a);
        if (b === null) b = normalize(row.b);
        return a.includes(term) || b.includes(term);
      });
    }

    function matchesFilters(row, filters) {
      return (!filters.severity || row.severity === filters.severity)
        && (!filters.scope || row.scope === filters.scope)
        && (!filters.difference || row.difference === filters.difference)
        && (!filters.table || (filters.table === '__none__' ? !row.tableLogical : row.tableLogical === filters.table))
        && (!filters.classification || (filters.classification === '__none__' ? !row.classification : row.classification === filters.classification))
        && (!filters.property || row.property === filters.property)
        && matchesSearch(row, filters.terms, filters.includeFullValues);
    }

    function currentFilters() {
      return {
        severity: elements.severityFilter.value,
        scope: elements.scopeFilter.value,
        difference: elements.differenceFilter.value,
        table: elements.tableFilter.value,
        classification: elements.classificationFilter.value,
        property: elements.propertyFilter.value,
        terms: normalize(elements.search.value).split(/\s+/).filter(Boolean),
        includeFullValues: elements.searchFullValues.checked
      };
    }

    function setFiltering(active, processed) {
      elements.filterProgress.classList.toggle('active', active);
      elements.filterProgress.setAttribute('aria-hidden', active ? 'false' : 'true');
      elements.resultsTable.setAttribute('aria-busy', active ? 'true' : 'false');
      if (active) elements.statusText.textContent = `Filtering ${formatNumber(processed)} of ${formatNumber(allRows.length)} rows…`;
    }

    function scheduleFilter(immediate) {
      clearTimeout(filterTimer);
      filterTimer = window.setTimeout(applyFilters, immediate ? 0 : 180);
    }

    function applyFilters() {
      const generation = ++filterGeneration;
      const filters = currentFilters();
      const output = [];
      const chunkSize = filters.includeFullValues ? 100 : 1000;
      let index = 0;
      setFiltering(true, 0);

      function processChunk() {
        if (generation !== filterGeneration) return;
        const end = Math.min(index + chunkSize, allRows.length);
        for (; index < end; index++) {
          const row = allRows[index];
          if (matchesFilters(row, filters)) output.push(row);
        }
        if (index < allRows.length) {
          setFiltering(true, index);
          window.requestAnimationFrame(processChunk);
          return;
        }
        filteredRows = output;
        currentPage = 1;
        sortRows();
        setFiltering(false, allRows.length);
        render();
      }

      window.requestAnimationFrame(processChunk);
    }

    function sortValue(row, key) {
      if (key === 'severityOrder') return row.severityOrder;
      return String(row[key] || '');
    }

    function sortRows() {
      filteredRows.sort((left, right) => {
        const a = sortValue(left, sortKey);
        const b = sortValue(right, sortKey);
        if (typeof a === 'number' && typeof b === 'number') return (a - b) * sortDirection;
        return String(a).localeCompare(String(b), undefined, {numeric:true,sensitivity:'base'}) * sortDirection;
      });
      document.querySelectorAll('th[data-key]').forEach(header => {
        if (header.dataset.key === sortKey) header.setAttribute('aria-sort', sortDirection === 1 ? 'ascending' : 'descending');
        else header.removeAttribute('aria-sort');
      });
    }

    function textCell(value, className) {
      const cell = document.createElement('td');
      if (className) cell.className = className;
      cell.textContent = value || '—';
      return cell;
    }

    function renderTable() {
      const pageCount = Math.max(1, Math.ceil(filteredRows.length / pageSize));
      currentPage = Math.min(Math.max(1, currentPage), pageCount);
      const start = (currentPage - 1) * pageSize;
      const pageRows = filteredRows.slice(start, start + pageSize);
      const fragment = document.createDocumentFragment();
      if (!pageRows.length) {
        const row = document.createElement('tr');
        const cell = document.createElement('td');
        cell.colSpan = 13;
        cell.className = 'empty';
        cell.textContent = 'No differences match the current filters.';
        row.appendChild(cell);
        fragment.appendChild(row);
      } else {
        pageRows.forEach(item => {
          const row = document.createElement('tr');
          row.dataset.id = item.id;
          if (selectedRow && selectedRow.id === item.id) row.classList.add('selected');
          const severityCell = document.createElement('td');
          const badge = document.createElement('span');
          badge.className = `badge ${item.severity}`;
          badge.textContent = item.severity;
          severityCell.appendChild(badge);
          row.appendChild(severityCell);
          row.appendChild(textCell(item.scopeDisplay));
          row.appendChild(textCell(item.difference));
          row.appendChild(textCell(displayTable(item)));
          row.appendChild(textCell(item.classification));
          row.appendChild(textCell(item.componentA));
          row.appendChild(textCell(item.componentAId));
          row.appendChild(textCell(item.componentB));
          row.appendChild(textCell(item.componentBId));
          row.appendChild(textCell(item.property));
          row.appendChild(textCell(item.aPreview));
          row.appendChild(textCell(item.bPreview));
          const actionCell = document.createElement('td');
          actionCell.className = 'action-column';
          if (item.inspectable) {
            const inspect = document.createElement('button');
            inspect.type = 'button';
            inspect.className = 'inspect';
            inspect.textContent = 'Inspect';
            inspect.addEventListener('click', () => showDetails(item));
            actionCell.appendChild(inspect);
            row.addEventListener('dblclick', () => showDetails(item));
          } else {
            actionCell.classList.add('not-inspectable');
            actionCell.textContent = '—';
            actionCell.title = 'Presence-only differences do not have two component definitions to inspect.';
          }
          row.appendChild(actionCell);
          fragment.appendChild(row);
        });
      }
      elements.resultsBody.replaceChildren(fragment);
      elements.pageStatus.textContent = `Page ${formatNumber(currentPage)} of ${formatNumber(pageCount)} · rows ${filteredRows.length ? formatNumber(start + 1) : '0'}–${formatNumber(Math.min(start + pageSize, filteredRows.length))}`;
      elements.firstPage.disabled = currentPage <= 1;
      elements.previousPage.disabled = currentPage <= 1;
      elements.nextPage.disabled = currentPage >= pageCount;
      elements.lastPage.disabled = currentPage >= pageCount;
    }

    function renderSummary() {
      elements.totalCount.textContent = formatNumber(allRows.length);
      elements.filteredCount.textContent = formatNumber(filteredRows.length);
      let critical = 0;
      let high = 0;
      let missingB = 0;
      filteredRows.forEach(row => {
        if (row.severity === 'Critical') critical++;
        if (row.severity === 'High') high++;
        if (row.difference === 'Missing in Environment B') missingB++;
      });
      elements.criticalCount.textContent = formatNumber(critical);
      elements.highCount.textContent = formatNumber(high);
      elements.missingBCount.textContent = formatNumber(missingB);
      elements.statusText.textContent = `Showing ${formatNumber(filteredRows.length)} of ${formatNumber(allRows.length)} differences. Only the current page is rendered.`;
    }

    function render() { renderSummary(); renderTable(); }

    function metaItem(label, value) {
      const item = document.createElement('div');
      item.className = 'meta-item';
      const caption = document.createElement('span');
      caption.textContent = label;
      const content = document.createElement('strong');
      content.textContent = value || '—';
      content.title = value || '';
      item.append(caption, content);
      return item;
    }

    function reportDefinitionFor(row) {
      return row && row.scope === 'Report'
        ? reportDefinitions.get(String(row.componentKey || '').toLocaleLowerCase())
        : null;
    }

    function configureValueSources(row) {
      const options = [createOption('property', 'Compared property values')];
      const definition = reportDefinitionFor(row);
      if (definition && (definition.aRdl || definition.bRdl)) options.push(createOption('report-rdl', 'Report RDL XML (normalized)'));
      if (definition && (definition.aRawRdl || definition.bRawRdl)) options.push(createOption('report-raw-rdl', 'Report RDL XML (raw retrieval)'));
      elements.valueSource.replaceChildren(...options);
      elements.valueSource.value = row.scope === 'Report' && row.property === 'RDL' && options.some(option => option.value === 'report-rdl')
        ? 'report-rdl'
        : 'property';
    }

    function currentValuePair() {
      if (!selectedRow) return {a:'', b:'', suffix:'value', label:'Compared property values'};
      const source = elements.valueSource.value;
      const definition = reportDefinitionFor(selectedRow);
      if (source === 'report-rdl' && definition) {
        return {a:definition.aRdl || '', b:definition.bRdl || '', suffix:'report-rdl-normalized', label:'Report RDL XML (normalized)'};
      }
      if (source === 'report-raw-rdl' && definition) {
        return {a:definition.aRawRdl || '', b:definition.bRawRdl || '', suffix:'report-rdl-raw', label:'Report RDL XML (raw retrieval)'};
      }
      return {a:selectedRow.a || '', b:selectedRow.b || '', suffix:'property-value', label:'Compared property values'};
    }

    function updateValueViewer() {
      const values = currentValuePair();
      elements.valueA.textContent = values.a;
      elements.valueB.textContent = values.b;
      elements.lengthA.textContent = `${formatNumber(values.a.length)} characters`;
      elements.lengthB.textContent = `${formatNumber(values.b.length)} characters`;
      showValuePanels();
    }

    function showValuePanels() {
      diffGeneration++;
      elements.definitionGrid.hidden = false;
      elements.diffPanel.hidden = true;
      elements.diffOutput.replaceChildren();
      elements.showDiff.textContent = enhancedDiffState === 'loaded'
        ? 'Show enhanced diff'
        : enhancedDiffState === 'failed'
          ? 'Enhanced diff unavailable'
          : 'Loading enhanced diff…';
    }

    function showDiffPanel() {
      elements.definitionGrid.hidden = true;
      elements.diffPanel.hidden = false;
      elements.showDiff.textContent = 'Show normal values';
    }

    function showDetails(row) {
      selectedRow = row;
      elements.detailHeading.textContent = `${row.scopeDisplay}: ${row.componentA || row.componentB || row.tableDisplay || row.property}`;
      elements.detailDescription.textContent = row.details;
      elements.drawerMeta.replaceChildren(
        metaItem('Severity', row.severity),
        metaItem('Difference', row.difference),
        metaItem('Table', displayTable(row)),
        metaItem('Component A', row.componentA),
        metaItem('Component A (ID)', row.componentAId),
        metaItem('Component B', row.componentB),
        metaItem('Component B (ID)', row.componentBId),
        metaItem('Property', row.property)
      );
      configureValueSources(row);
      updateValueViewer();
      elements.backdrop.hidden = false;
      elements.drawer.hidden = false;
      document.body.style.overflow = 'hidden';
      window.requestAnimationFrame(applyValuePaneSplit);
      elements.closeDrawer.focus();
      renderTable();
    }

    function closeDetails() {
      elements.backdrop.hidden = true;
      elements.drawer.hidden = true;
      document.body.style.overflow = '';
      elements.valueA.textContent = '';
      elements.valueB.textContent = '';
      elements.diffOutput.replaceChildren();
      diffGeneration++;
      selectedRow = null;
      renderTable();
    }

    function loadStyle(url) {
      return new Promise((resolve, reject) => {
        const existing = document.querySelector(`link[data-cdn-url='${url}']`);
        if (existing) {
          if (existing.dataset.cdnState === 'loaded') resolve();
          else {
            existing.addEventListener('load', resolve, {once:true});
            existing.addEventListener('error', () => reject(new Error(`Could not load ${url}`)), {once:true});
          }
          return;
        }
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = url;
        link.dataset.cdnUrl = url;
        link.dataset.cdnState = 'loading';
        link.referrerPolicy = 'no-referrer';
        link.onload = () => { link.dataset.cdnState = 'loaded'; resolve(); };
        link.onerror = () => { link.remove(); reject(new Error(`Could not load ${url}`)); };
        document.head.appendChild(link);
      });
    }

    function loadScript(url) {
      return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[data-cdn-url='${url}']`);
        if (existing) {
          if (existing.dataset.cdnState === 'loaded') resolve();
          else {
            existing.addEventListener('load', resolve, {once:true});
            existing.addEventListener('error', () => reject(new Error(`Could not load ${url}`)), {once:true});
          }
          return;
        }
        const script = document.createElement('script');
        script.src = url;
        script.dataset.cdnUrl = url;
        script.dataset.cdnState = 'loading';
        script.referrerPolicy = 'no-referrer';
        script.onload = () => { script.dataset.cdnState = 'loaded'; resolve(); };
        script.onerror = () => { script.remove(); reject(new Error(`Could not load ${url}`)); };
        document.head.appendChild(script);
      });
    }

    async function loadEnhancedDiff() {
      if (enhancedDiffState === 'loaded' || enhancedDiffState === 'loading') return;
      enhancedDiffState = 'loading';
      elements.showDiff.disabled = true;
      elements.showDiff.textContent = 'Loading enhanced diff…';
      elements.loadDiffLibraries.disabled = true;
      elements.loadDiffLibraries.hidden = true;
      elements.cdnStatus.textContent = 'Loading pinned CDN libraries…';
      try {
        await Promise.all([
          loadStyle(diff2HtmlCssUrl),
          loadScript(jsDiffUrl),
          loadScript(diff2HtmlUrl)
        ]);
        if (!window.Diff || !window.Diff2Html) throw new Error('The CDN files loaded without the expected browser APIs.');
        enhancedDiffState = 'loaded';
        elements.diffFormat.disabled = false;
        elements.showDiff.disabled = false;
        if (elements.diffPanel.hidden) elements.showDiff.textContent = 'Show enhanced diff';
        elements.cdnStatus.textContent = 'Enhanced diff ready';
      } catch (error) {
        enhancedDiffState = 'failed';
        elements.showDiff.disabled = true;
        elements.showDiff.textContent = 'Enhanced diff unavailable';
        elements.loadDiffLibraries.disabled = false;
        elements.loadDiffLibraries.hidden = false;
        elements.loadDiffLibraries.textContent = 'Retry CDN diff viewer';
        elements.cdnStatus.textContent = 'Could not load CDN libraries. Side-by-side values still work.';
        showToast('CDN diff viewer could not be loaded');
      }
    }

    function prepareForDiff(value) {
      const normalized = String(value || '').replace(/\r\n?/g, '\n');
      return normalized.trimStart().startsWith('<')
        ? normalized.replace(/>\s*</g, '>\n<')
        : normalized;
    }

    function createPatch(a, b) {
      return new Promise((resolve, reject) => {
        try {
          window.Diff.createTwoFilesPatch(
            environmentAName,
            environmentBName,
            a,
            b,
            '',
            '',
            {context:5, maxEditLength:100000, callback:resolve});
        } catch (error) { reject(error); }
      });
    }

    async function renderEnhancedDiff() {
      if (!selectedRow || enhancedDiffState !== 'loaded') return;
      const rowAtStart = selectedRow;
      const generation = ++diffGeneration;
      const values = currentValuePair();
      showDiffPanel();
      const placeholder = document.createElement('div');
      placeholder.className = 'diff-placeholder';
      placeholder.textContent = `Building ${values.label} diff…`;
      elements.diffOutput.replaceChildren(placeholder);
      await new Promise(resolve => window.setTimeout(resolve, 0));
      try {
        const patch = await createPatch(prepareForDiff(values.a), prepareForDiff(values.b));
        if (generation !== diffGeneration || selectedRow !== rowAtStart || elements.diffPanel.hidden) return;
        if (!patch) {
          placeholder.textContent = 'The values are too different to calculate safely. Use the complete side-by-side viewer or download the values.';
          return;
        }
        elements.diffOutput.innerHTML = window.Diff2Html.html(patch, {
          drawFileList:false,
          matching:'lines',
          outputFormat:elements.diffFormat.value
        });
      } catch (error) {
        if (generation === diffGeneration) placeholder.textContent = 'The enhanced diff could not be generated. The complete side-by-side values remain available.';
      }
    }

    function toggleEnhancedDiff() {
      if (elements.diffPanel.hidden) renderEnhancedDiff();
      else showValuePanels();
    }

    async function copyValue(value, label) {
      try {
        if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(value);
        else {
          const area = document.createElement('textarea');
          area.value = value;
          area.style.position = 'fixed';
          area.style.opacity = '0';
          document.body.appendChild(area);
          area.select();
          document.execCommand('copy');
          area.remove();
        }
        showToast(`${label} copied`);
      } catch (error) { showToast('Copy was blocked by the browser'); }
    }

    function downloadValue(value, suffix) {
      const blob = new Blob([value], {type:'text/plain;charset=utf-8'});
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = `comparison-row-${selectedRow ? selectedRow.id : 'value'}-${suffix}.txt`;
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(link.href), 1000);
    }

    let toastTimer = 0;
    function showToast(message) {
      clearTimeout(toastTimer);
      elements.toast.textContent = message;
      elements.toast.hidden = false;
      toastTimer = window.setTimeout(() => { elements.toast.hidden = true; }, 2200);
    }

    function clearFilters() {
      elements.search.value = '';
      elements.searchFullValues.checked = false;
      elements.severityFilter.value = '';
      elements.scopeFilter.value = '';
      elements.differenceFilter.value = '';
      elements.tableFilter.value = '';
      elements.classificationFilter.value = '';
      elements.propertyFilter.value = '';
      scheduleFilter(true);
    }

    document.querySelectorAll('[data-sort]').forEach(button => button.addEventListener('click', () => {
      const nextKey = button.dataset.sort;
      if (sortKey === nextKey) sortDirection *= -1;
      else { sortKey = nextKey; sortDirection = 1; }
      currentPage = 1;
      sortRows();
      render();
    }));
    ['severityFilter','scopeFilter','differenceFilter','tableFilter','classificationFilter','propertyFilter','searchFullValues'].forEach(id => elements[id].addEventListener('change', () => scheduleFilter(true)));
    elements.search.addEventListener('input', () => scheduleFilter(false));
    elements.clearFilters.addEventListener('click', clearFilters);
    elements.resetColumns.addEventListener('click', resetColumnWidths);
    elements.pageSize.addEventListener('change', () => { pageSize = Number(elements.pageSize.value); currentPage = 1; renderTable(); });
    elements.firstPage.addEventListener('click', () => { currentPage = 1; renderTable(); });
    elements.previousPage.addEventListener('click', () => { currentPage--; renderTable(); });
    elements.nextPage.addEventListener('click', () => { currentPage++; renderTable(); });
    elements.lastPage.addEventListener('click', () => { currentPage = Math.max(1, Math.ceil(filteredRows.length / pageSize)); renderTable(); });
    elements.closeDrawer.addEventListener('click', closeDetails);
    elements.backdrop.addEventListener('click', closeDetails);
    elements.valueSource.addEventListener('change', updateValueViewer);
    elements.loadDiffLibraries.addEventListener('click', loadEnhancedDiff);
    elements.showDiff.addEventListener('click', toggleEnhancedDiff);
    elements.diffFormat.addEventListener('change', () => { if (!elements.diffPanel.hidden) renderEnhancedDiff(); });
    elements.wrapToggle.addEventListener('click', () => { const nowrap = elements.drawer.classList.toggle('nowrap'); elements.wrapToggle.textContent = nowrap ? 'Enable wrapping' : 'Disable wrapping'; });
    elements.copyA.addEventListener('click', () => copyValue(currentValuePair().a, 'Environment A value'));
    elements.copyB.addEventListener('click', () => copyValue(currentValuePair().b, 'Environment B value'));
    elements.downloadA.addEventListener('click', () => { const values = currentValuePair(); downloadValue(values.a, `${values.suffix}-environment-a`); });
    elements.downloadB.addEventListener('click', () => { const values = currentValuePair(); downloadValue(values.b, `${values.suffix}-environment-b`); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && !elements.drawer.hidden) closeDetails(); });

    initializeFilters();
    initializeColumnResizing();
    initializeValuePaneResizing();
    loadEnhancedDiff();
    applyFilters();
  </script>
</body>
</html>
";
    }
}
