using System.Windows;

using DotZpl.Viewer.ViewModels;

namespace DotZpl.Viewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
