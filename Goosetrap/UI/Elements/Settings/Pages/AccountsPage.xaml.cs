using System;
using Goosetrap.UI.ViewModels.Settings;

namespace Goosetrap.UI.Elements.Settings.Pages
{
    public partial class AccountsPage
    {
        public AccountsPage()
        {
            var viewModel = new AccountsViewModel();
            DataContext = viewModel;
            InitializeComponent();

            Unloaded += (s, e) => viewModel.StopTimer();
        }
    }
}
