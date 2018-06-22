using Dupe_Finder_DB;
using Dupe_Finder_VM;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Dupe_Finder_UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void miOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                // Set validate names and check file exists to false otherwise windows will
                // not let you select "Folder Selection."
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                // Always default to Folder Selection.
                FileName = "Folder Selection."
            };
            if (ofd.ShowDialog() == true)
            {
                string path = Path.GetDirectoryName(ofd.FileName);
                lblFolder.Content = path;
                lblStatus.Content = "Working...";
                var result = await Operations.GetBasicComparison(path);
                tvDupesList.Items.Clear();
                foreach (var item in result.TreeViewItems)
                {
                    tvDupesList.Items.Add(item);
                }
                lblDuplicateCount.Content = $"Extra Files: {result.DuplicateItemCount} files in {result.TreeViewItems.Count().ToString("N0")} groups";
                lblWastedSpace.Content = $"Wasted Space: {result.WastedSpace.ToString("N0")} bytes";
                miStartChecksumComparison.IsEnabled = true;
                lblStatus.Content = "Done";
            }
        }

        private async void miStartChecksumComparison_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Content = "Working...";
            var result = await Operations.GetFullComparison();
            tvDupesList.Items.Clear();
            foreach (var item in result.TreeViewItems)
            {
                tvDupesList.Items.Add(item);
            }
            lblDuplicateCount.Content = $"Extra Files: {result.DuplicateItemCount} files in {result.TreeViewItems.Count().ToString("N0")} groups";
            lblWastedSpace.Content = $"Wasted Space: {result.WastedSpace.ToString("N0")} bytes";
            miStartChecksumComparison.IsEnabled = true;
            lblStatus.Content = "Done";
        }
    }
}
