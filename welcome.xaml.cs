using ChatAppGenAI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Store;
using static ChatAppGenAI.MainWindow;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Dolphy_AI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class welcome : Window
    {


        public welcome()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            var presenter = this.AppWindow.Presenter as OverlappedPresenter;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = false;
            presenter.IsResizable = false;
            welcomedolphy.Text = "Hello, " + Environment.UserName + ". What should we do today?";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new MainWindow().Activate();
            Close();
        }
    }
}
