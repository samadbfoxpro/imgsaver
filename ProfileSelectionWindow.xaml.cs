using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    public partial class ProfileSelectionWindow : Window
    {
        public BrowserProfile? SelectedProfile { get; private set; }
        private List<BrowserProfile> _profiles = new();

        public ProfileSelectionWindow()
        {
            InitializeComponent();
            LoadProfilesList();
            ChkAlwaysAsk.IsChecked = ProfileManager.AlwaysAskAccountOnStartup;

            this.KeyDown += ProfileSelectionWindow_KeyDown;
        }

        private void LoadProfilesList()
        {
            _profiles = ProfileManager.LoadProfiles();
            LstProfiles.ItemsSource = null;
            LstProfiles.ItemsSource = _profiles;

            var active = ProfileManager.GetActiveProfile();
            var found = _profiles.FirstOrDefault(p => p.Id == active.Id);
            if (found != null)
            {
                LstProfiles.SelectedItem = found;
            }
            else if (_profiles.Count > 0)
            {
                LstProfiles.SelectedIndex = 0;
            }
        }

        private void ProfileSelectionWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            else if (e.Key == System.Windows.Input.Key.Enter)
            {
                LaunchSelected();
            }
            else if (e.Key >= System.Windows.Input.Key.D1 && e.Key <= System.Windows.Input.Key.D9)
            {
                int index = e.Key - System.Windows.Input.Key.D1;
                if (index >= 0 && index < _profiles.Count)
                {
                    LstProfiles.SelectedIndex = index;
                    LaunchSelected();
                }
            }
        }

        private void LstProfiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LaunchSelected();
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            LaunchSelected();
        }

        private void LaunchSelected()
        {
            if (LstProfiles.SelectedItem is BrowserProfile profile)
            {
                SelectedProfile = profile;
                ProfileManager.AlwaysAskAccountOnStartup = ChkAlwaysAsk.IsChecked == true;
                ProfileManager.SetActiveProfile(profile);
                DialogResult = true;
                Close();
            }
            else
            {
                DarkConfirmDialog.ShowMessage("Selection Required", "Please select an Account Profile first.", this);
            }
        }

        private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProfileEditDialog($"Account {_profiles.Count + 1}", "👤", "#2ECC71", "Add Account Profile")
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var newProfile = new BrowserProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = dialog.AccountName,
                    Icon = dialog.SelectedIcon,
                    ColorHex = dialog.SelectedColor,
                    LastUsed = DateTime.Now
                };
                _profiles.Add(newProfile);
                ProfileManager.SaveProfiles(_profiles);
                LoadProfilesList();
                LstProfiles.SelectedItem = newProfile;
            }
        }

        private void BtnEditAccount_Click(object sender, RoutedEventArgs e)
        {
            if (LstProfiles.SelectedItem is BrowserProfile profile)
            {
                var dialog = new ProfileEditDialog(profile.Name, profile.Icon, profile.ColorHex, "Edit Account Profile")
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    profile.Name = dialog.AccountName;
                    profile.Icon = dialog.SelectedIcon;
                    profile.ColorHex = dialog.SelectedColor;
                    ProfileManager.SaveProfiles(_profiles);
                    LoadProfilesList();
                    LstProfiles.SelectedItem = profile;
                }
            }
            else
            {
                DarkConfirmDialog.ShowMessage("Selection Required", "Please select an Account Profile to edit.", this);
            }
        }

        private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_profiles.Count <= 1)
            {
                DarkConfirmDialog.ShowMessage("Delete Account", "You must keep at least one Account Profile.", this);
                return;
            }

            if (LstProfiles.SelectedItem is BrowserProfile profile)
            {
                bool confirmed = DarkConfirmDialog.ShowConfirm(
                    "Delete Account Profile",
                    $"Are you sure you want to delete profile '{profile.Name}'?\n(Note: Existing session data in data/profiles will remain untouched)",
                    this,
                    true,
                    "Delete",
                    "Cancel"
                );

                if (confirmed)
                {
                    _profiles.Remove(profile);
                    ProfileManager.SaveProfiles(_profiles);
                    LoadProfilesList();
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
