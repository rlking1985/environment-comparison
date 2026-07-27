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
