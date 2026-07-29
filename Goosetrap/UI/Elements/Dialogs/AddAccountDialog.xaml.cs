using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Goosetrap.Utility;

namespace Goosetrap.UI.Elements.Dialogs
{
    public partial class AddAccountDialog
    {
        public string Cookie { get; private set; } = "";
        public string Username { get; private set; } = "";
        public string DisplayName { get; private set; } = "";
        public long UserId { get; private set; }
        public string AvatarUri { get; private set; } = "";

        public AddAccountDialog()
        {
            InitializeComponent();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string rawCookie = CookieTextBox.Text.Trim();
            
            // Clean up the cookie string if they pasted the whole Cookie header
            if (rawCookie.Contains(".ROBLOSECURITY="))
            {
                var match = Regex.Match(rawCookie, @"\.ROBLOSECURITY=(_\|[^;]+)");
                if (match.Success)
                    rawCookie = match.Groups[1].Value;
            }

            if (string.IsNullOrEmpty(rawCookie))
            {
                ShowError("Куки не может быть пустым!");
                return;
            }

            AddButton.IsEnabled = false;
            ShowError("");

            var (success, username, displayName, userId) = await AccountsHelper.ValidateCookieAsync(rawCookie);
            if (!success)
            {
                ShowError("Не удалось авторизовать куки. Проверьте правильность ввода!");
                AddButton.IsEnabled = true;
                return;
            }

            // Check if account already exists
            if (App.Accounts.Prop.Accounts.Any(x => x.UserId == userId))
            {
                ShowError("Этот аккаунт уже добавлен!");
                AddButton.IsEnabled = true;
                return;
            }

            Cookie = rawCookie;
            Username = username;
            DisplayName = displayName;
            UserId = userId;
            AvatarUri = await AccountsHelper.FetchAvatarUriAsync(userId);

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                ErrorTextBlock.Text = message;
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }
    }
}
