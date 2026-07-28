using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EnvironmentComparison.Ui;
using XrmToolBox.Extensibility;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class PluginMetadataTests
    {
        [TestMethod]
        public void PluginExportsRequiredNameDescriptionAndImages()
        {
            var metadata = typeof(EnvironmentComparisonPlugin)
                .GetCustomAttributes(typeof(ExportMetadataAttribute), false)
                .Cast<ExportMetadataAttribute>()
                .ToDictionary(attribute => attribute.Name, attribute => attribute.Value);

            Assert.AreEqual("Environment Comparison", metadata["Name"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(metadata["Description"] as string));
            Assert.IsTrue((metadata["SmallImageBase64"] as string)?.Length > 100);
            Assert.IsTrue((metadata["BigImageBase64"] as string)?.Length > 100);
        }

        [TestMethod]
        public void PluginControlCanBeCreatedWithoutAConnection()
        {
            using (var control = (EnvironmentComparisonControl)new EnvironmentComparisonPlugin().GetControl())
            {
                Assert.IsNotNull(control);
                var grid = (DataGridView)typeof(EnvironmentComparisonControl)
                    .GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(control)!;
                Assert.AreEqual("Table classification", grid.Columns["Classification"].HeaderText);
                var rawExportButton = (Button)typeof(EnvironmentComparisonControl)
                    .GetField("_rawExportButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(control)!;
                Assert.AreEqual("Export raw metadata (JSON)", rawExportButton.Text);
                Assert.IsFalse(rawExportButton.Enabled);
                var htmlExportButton = (Button)typeof(EnvironmentComparisonControl)
                    .GetField("_htmlExportButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(control)!;
                Assert.AreEqual("Export filterable HTML", htmlExportButton.Text);
                Assert.IsFalse(htmlExportButton.Enabled);
                var reportsCheckBox = (CheckBox)typeof(EnvironmentComparisonControl)
                    .GetField("_reportsCheckBox", BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(control)!;
                Assert.AreEqual("SSRS reports", reportsCheckBox.Text);
                Assert.IsTrue(reportsCheckBox.Checked);
                var tableRegexBox = (TextBox)typeof(EnvironmentComparisonControl)
                    .GetField("_tableLogicalNameRegexBox", BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(control)!;
                Assert.IsTrue(tableRegexBox.Enabled);
                Assert.AreEqual("Table logical name regular expression", tableRegexBox.AccessibleName);
            }
        }

        [TestMethod]
        public void TableRegexIsDisabledForReportOnlyComparisons()
        {
            using (var control = new EnvironmentComparisonControl())
            {
                var type = typeof(EnvironmentComparisonControl);
                var tableRegexBox = (TextBox)type
                    .GetField("_tableLogicalNameRegexBox", BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(control)!;
                foreach (var fieldName in new[] { "_tablesCheckBox", "_columnsCheckBox", "_formsCheckBox", "_viewsCheckBox" })
                {
                    ((CheckBox)type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!).Checked = false;
                }

                Assert.IsFalse(tableRegexBox.Enabled);

                ((CheckBox)type.GetField("_viewsCheckBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!).Checked = true;
                Assert.IsTrue(tableRegexBox.Enabled);
            }
        }

        [TestMethod]
        public void CompareWithoutConnectionsRequestsEnvironmentA()
        {
            using (var control = new EnvironmentComparisonControl())
            {
                RequestConnectionEventArgs? request = null;
                control.OnRequestConnection += (_, eventArgs) => request = eventArgs as RequestConnectionEventArgs;

                typeof(EnvironmentComparisonControl)
                    .GetMethod("BeginComparison", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(control, null);

                Assert.IsNotNull(request);
                Assert.AreEqual(string.Empty, request.ActionName);
                Assert.AreSame(control, request.Control);
            }
        }

        [TestMethod]
        public void ConnectionFlowRequiresEnvironmentAThenEnvironmentB()
        {
            Assert.AreEqual(
                EnvironmentComparisonControl.ComparisonConnectionRequirement.EnvironmentA,
                EnvironmentComparisonControl.GetConnectionRequirement(false, 0));
            Assert.AreEqual(
                EnvironmentComparisonControl.ComparisonConnectionRequirement.EnvironmentB,
                EnvironmentComparisonControl.GetConnectionRequirement(true, 0));
            Assert.AreEqual(
                EnvironmentComparisonControl.ComparisonConnectionRequirement.None,
                EnvironmentComparisonControl.GetConnectionRequirement(true, 1));
        }

        [TestMethod]
        public void EnvironmentCardsReflowAtNarrowWidths()
        {
            using (var control = new EnvironmentComparisonControl())
            {
                control.Size = new Size(1200, 800);
                control.CreateControl();
                control.PerformLayout();
                Assert.IsFalse(control.UsesCompactLayout);
                Assert.AreEqual(3, control.EnvironmentLayoutColumnCount);

                control.Size = new Size(640, 480);
                control.PerformLayout();
                Assert.IsTrue(control.UsesCompactLayout);
                Assert.AreEqual(1, control.EnvironmentLayoutColumnCount);
            }
        }
    }
}
