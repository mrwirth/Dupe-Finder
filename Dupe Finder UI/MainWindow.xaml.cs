using Dupe_Finder_UI.ViewModel;
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
        protected DupeFinderVM DuplicatesTreeVM { get; }

        public MainWindow()
        {
            InitializeComponent();
            DuplicatesTreeVM = new DupeFinderVM(new WpfIOService());
            DataContext = DuplicatesTreeVM;
        }

        private void TextBlockCopy_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void TextBlockCopy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is DuplicateFileVM duplicateFile)
            {
                Clipboard.SetDataObject(duplicateFile.Path);
            }
        }
    }
}
