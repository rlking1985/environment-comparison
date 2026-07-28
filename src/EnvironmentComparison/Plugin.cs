using System;
using System.ComponentModel.Composition;
using EnvironmentComparison.Ui;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace EnvironmentComparison
{
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "Environment Comparison")]
    [ExportMetadata("Description", "Compare Dataverse tables, columns, forms, system views, and SSRS reports between environments")]
    [ExportMetadata("SmallImageBase64", PluginImageData.Small)]
    [ExportMetadata("BigImageBase64", PluginImageData.Big)]
    [ExportMetadata("BackgroundColor", "#334155")]
    [ExportMetadata("PrimaryFontColor", "White")]
    [ExportMetadata("SecondaryFontColor", "#E2E8F0")]
    public sealed class EnvironmentComparisonPlugin : PluginBase, INoConnectionRequired
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new EnvironmentComparisonControl();
        }

        public override Guid GetId()
        {
            return new Guid("8E48744D-4A02-46D8-AAB6-AB927717327C");
        }
    }
}
