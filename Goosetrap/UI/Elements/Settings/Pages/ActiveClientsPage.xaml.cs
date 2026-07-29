using System;
using Goosetrap.UI.ViewModels.Settings;

namespace Goosetrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ActiveClientsPage.xaml
    /// </summary>
    public partial class ActiveClientsPage
    {
        public ActiveClientsPage()
        {
            App.Logger.WriteLine("ActiveClientsPage", "Constructor started");
            var viewModel = new ActiveClientsViewModel();
            DataContext = viewModel;
            InitializeComponent();
            App.Logger.WriteLine("ActiveClientsPage", "Constructor finished");

            Unloaded += (s, e) => viewModel.StopTimer();
        }
    }
}
