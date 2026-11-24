using System.IO;
using System.Windows;
using Microsoft.Win32;
using TidyFlow.App.ViewModels;
using Wpf.Ui.Controls;

namespace TidyFlow.App;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void BrowseOrganizeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to organize",
            InitialDirectory = Directory.Exists(ViewModel.Organize.FolderPath)
                ? ViewModel.Organize.FolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.Organize.FolderPath = dialog.FolderName;
            ViewModel.Organize.ScanAndPreview();
        }
    }

    private void ApplyOrganize_Click(object sender, RoutedEventArgs e)
    {
        var plan = ViewModel.Organize.CurrentPlan;
        if (plan is null || plan.ReadyCount == 0)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Move {plan.ReadyCount} file(s) according to the preview?",
            "Confirm Organize",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm == System.Windows.MessageBoxResult.Yes && ViewModel.Organize.ApplyCommand.CanExecute(null))
        {
            ViewModel.Organize.ApplyCommand.Execute(null);
        }
    }

    private void AddRenameFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select files to batch-rename",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.Rename.AddPaths(dialog.FileNames);
        }
    }

    private void AddRenameFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Load all files from a folder"
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.Rename.AddPaths(new[] { dialog.FolderName });
        }
    }

    private void BrowseRenameOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select destination folder for renamed files",
            InitialDirectory = Directory.Exists(ViewModel.Rename.OutputFolderPath)
                ? ViewModel.Rename.OutputFolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.Rename.OutputFolderPath = dialog.FolderName;
        }
    }

    private void ApplyRename_Click(object sender, RoutedEventArgs e)
    {
        var plan = ViewModel.Rename.CurrentPlan;
        if (plan is null || plan.ReadyCount == 0)
        {
            return;
        }

        if (plan.HasErrors)
        {
            System.Windows.MessageBox.Show(
                "Please resolve highlighted errors in the preview before applying.",
                "Cannot Apply Rename",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Rename {plan.ReadyCount} file(s) according to the preview?",
            "Confirm Batch Rename",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm == System.Windows.MessageBoxResult.Yes && ViewModel.Rename.ApplyCommand.CanExecute(null))
        {
            ViewModel.Rename.ApplyCommand.Execute(null);
        }
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OrganizeArea_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0)
        {
            return;
        }

        var folder = Directory.Exists(dropped[0])
            ? dropped[0]
            : Path.GetDirectoryName(dropped[0]);

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            ViewModel.Organize.FolderPath = folder;
            ViewModel.Organize.ScanAndPreview();
        }
    }

    private void RenameArea_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped && dropped.Length > 0)
        {
            ViewModel.Rename.AddPaths(dropped);
        }
    }

    private void BrowseDuplicatesFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder to scan for duplicate files",
            InitialDirectory = Directory.Exists(ViewModel.Duplicates.FolderPath)
                ? ViewModel.Duplicates.FolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.Duplicates.FolderPath = dialog.FolderName;
            ViewModel.Duplicates.ScanAsync();
        }
    }

    private void DeleteDuplicateFiles_Click(object sender, RoutedEventArgs e)
    {
        var count = ViewModel.Duplicates.CheckedCount;
        if (count == 0)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Move {count} checked duplicate file(s) to the Windows Recycle Bin?",
            "Confirm Recycle Bin Move",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm == System.Windows.MessageBoxResult.Yes && ViewModel.Duplicates.DeleteCheckedCommand.CanExecute(null))
        {
            ViewModel.Duplicates.DeleteCheckedCommand.Execute(null);
        }
    }

    private void BrowseSpaceFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder to analyze disk usage",
            InitialDirectory = Directory.Exists(ViewModel.SpaceCleaner.FolderPath)
                ? ViewModel.SpaceCleaner.FolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.SpaceCleaner.FolderPath = dialog.FolderName;
            ViewModel.SpaceCleaner.ScanAsync();
        }
    }

    private void DeleteLargeFiles_Click(object sender, RoutedEventArgs e)
    {
        var selectedCount = ViewModel.SpaceCleaner.LargestFiles.Count(f => f.IsSelectedForDeletion);
        if (selectedCount == 0)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Move {selectedCount} selected file(s) to the Windows Recycle Bin?",
            "Confirm Recycle Bin Move",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm == System.Windows.MessageBoxResult.Yes && ViewModel.SpaceCleaner.DeleteSelectedFilesCommand.CanExecute(null))
        {
            ViewModel.SpaceCleaner.DeleteSelectedFilesCommand.Execute(null);
        }
    }

    private void CleanEmptyFolders_Click(object sender, RoutedEventArgs e)
    {
        var count = ViewModel.SpaceCleaner.EmptyFolderCount;
        if (count == 0)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Remove {count} empty subfolder(s)? Only folders containing zero files will be removed.",
            "Confirm Empty Folder Cleanup",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm == System.Windows.MessageBoxResult.Yes && ViewModel.SpaceCleaner.CleanEmptyFoldersCommand.CanExecute(null))
        {
            ViewModel.SpaceCleaner.CleanEmptyFoldersCommand.Execute(null);
        }
    }
}
