using System.Windows;

using WpfZpl.Viewer.ViewModels;

namespace WpfZpl.Viewer
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
