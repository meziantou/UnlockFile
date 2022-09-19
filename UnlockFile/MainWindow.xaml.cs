using Meziantou.Framework.Win32;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;

namespace UnlockFile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const string DefaultTitle = "Unlock File";

        public MainWindow()
        {
            Title = DefaultTitle;
            InitializeComponent();
            MenuItemRestartAsAdmin.IsEnabled = CanRunAsAdmin();
            UpdateShellIntegrationMenuItems();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var arg = Environment.GetCommandLineArgs().LastOrDefault();
            if (!string.IsNullOrEmpty(arg))
            {
                OpenPath(arg);
            }
        }

        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenPath(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(path + " is not a valid file path");
                return;
            }

            Title = DefaultTitle + " - " + path;
            ListViewProcess.Items.Clear();

            try
            {
                using var session = RestartManager.CreateSession();
                session.RegisterFile(path);
                var processes = session.GetProcessesLockingResources();
                if (processes.Count == 0)
                {
                    MessageBox.Show("The file is not locked");
                }
                else
                {
                    foreach (var process in processes)
                    {
                        ListViewProcess.Items.Add(process);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occured: " + ex);
            }
        }

        private static bool CanRunAsAdmin()
        {
            using (var token = AccessToken.OpenCurrentProcessToken(TokenAccessLevels.Query))
            {
                if (token.GetElevationType() == TokenElevationType.Limited)
                {
                    return true;
                }
            }

            return false;
        }

        private void MenuItemRestartAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            string commandLine;
            using (var searcher = new ManagementObjectSearcher("SELECT Name, CommandLine FROM Win32_Process WHERE ProcessId = " + Process.GetCurrentProcess().Id))
            using (var objects = searcher.Get())
            {
                commandLine = objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
            }

            var (fileName, arguments) = SplitCommandLine(commandLine);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
            };
            Process.Start(psi);
            Close();

            static (string fileName, string arguments) SplitCommandLine(string command)
            {
                var arr = command.Trim().Split(command.Trim()[0] == '"' ? '"' : ' ', 2, StringSplitOptions.RemoveEmptyEntries);

                return arr.Length > 1 ? (arr[0], arr[1]) : (arr[0], string.Empty);
            }
        }

        private void UpdateShellIntegrationMenuItems()
        {
            var isIntegrated = IsShellIntegrationPresent();
            MenuItemAddShellIntegration.Visibility = isIntegrated ? Visibility.Collapsed : Visibility.Visible;
            MenuItemRemoveShellIntegration.Visibility = !isIntegrated ? Visibility.Collapsed : Visibility.Visible;
        }

        private static bool IsShellIntegrationPresent()
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Classes\\*\\shell\\UnlockFile", writable: false);
            return key != null;
        }

        private void MenuItemAddShellIntegration_Click(object sender, RoutedEventArgs e)
        {
            using var reg = Registry.CurrentUser.CreateSubKey("Software\\Classes\\*\\shell\\UnlockFile", writable: true);
            reg.SetValue("", "Unlock File");

            using var command = reg.CreateSubKey("command");
            command.SetValue("", $"\"{Process.GetCurrentProcess().MainModule.FileName}\" \"%1\"");

            UpdateShellIntegrationMenuItems();
        }

        private void MenuItemRemoveShellIntegration_Click(object sender, RoutedEventArgs e)
        {
            Registry.CurrentUser.DeleteSubKeyTree("Software\\Classes\\*\\shell\\UnlockFile", throwOnMissingSubKey: false);

            UpdateShellIntegrationMenuItems();
        }

        private void ExecuteOpenCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                OpenPath(dialog.FileName);
            }
        }

        private void ExecuteDeleteCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            var process = ListViewProcess.SelectedItem as Process;
            if (process == null)
                return;

            var result = MessageBox.Show($"Are you sure you want to kill the process {process.ProcessName}?", "Kill process?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                process.Kill();
                ListViewProcess.Items.Remove(process);
            }
        }
    }
}
