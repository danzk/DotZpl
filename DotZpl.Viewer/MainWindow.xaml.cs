using System.Windows;

using DotZpl.Viewer.Shared.ViewModels;

namespace DotZpl.Viewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(new WpfDispatcher(), new WpfFileDialogService());
        }
    }
}
