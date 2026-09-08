using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Scadix.Designer.Views;

public partial class AttachToProcessDialog : Window
{
    private List<ProcessInfo> _allProcesses = new();
    private List<ProcessInfo> _filteredProcesses = new();

    public AttachToProcessDialog()
    {
        InitializeComponent();
        LoadProcesses();
    }

    private void LoadProcesses()
    {
        try
        {
            var showAll = ShowAllProcessesCheckBox?.IsChecked ?? false;
            var currentUser = Environment.UserName;

            _allProcesses = Process.GetProcesses()
                .Where(p => showAll || IsCurrentUserProcess(p))
                .Select(p => new ProcessInfo
                {
                    Id = p.Id,
                    ProcessName = p.ProcessName,
                    MainWindowTitle = GetWindowTitle(p),
                    ProcessType = GetProcessType(p),
                    UserName = GetProcessUser(p)
                })
                .OrderBy(p => p.ProcessName)
                .ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading processes: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        var filter = FilterTextBox?.Text?.ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(filter))
        {
            _filteredProcesses = _allProcesses;
        }
        else
        {
            _filteredProcesses = _allProcesses
                .Where(p => 
                    p.ProcessName.ToLowerInvariant().Contains(filter) ||
                    p.MainWindowTitle.ToLowerInvariant().Contains(filter) ||
                    p.Id.ToString().Contains(filter))
                .ToList();
        }

        if (ProcessDataGrid != null)
        {
            ProcessDataGrid.ItemsSource = _filteredProcesses;
        }
    }

    private bool IsCurrentUserProcess(Process process)
    {
        try
        {
            // Simple check - in production you'd want more robust user checking
            return true; // For now, show all processes
        }
        catch
        {
            return false;
        }
    }

    private string GetWindowTitle(Process process)
    {
        try
        {
            return string.IsNullOrWhiteSpace(process.MainWindowTitle) 
                ? "(No window)" 
                : process.MainWindowTitle;
        }
        catch
        {
            return "(Unknown)";
        }
    }

    private string GetProcessType(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName?.ToLowerInvariant() ?? "";
            
            if (fileName.Contains("dotnet") || fileName.EndsWith(".dll"))
                return ".NET";
            else if (fileName.EndsWith(".exe"))
                return "Native";
            else
                return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetProcessUser(Process process)
    {
        try
        {
            // Simplified - in production use proper WMI or P/Invoke
            return Environment.UserName;
        }
        catch
        {
            return "Unknown";
        }
    }

    private void FilterTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ShowAllProcesses_Changed(object? sender, RoutedEventArgs e)
    {
        LoadProcesses();
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        LoadProcesses();
    }

    private void ProcessDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Enable/disable Attach button based on selection
        if (AttachButton != null)
        {
            AttachButton.IsEnabled = ProcessDataGrid?.SelectedItem != null;
        }
    }

    private void Attach_Click(object? sender, RoutedEventArgs e)
    {
        if (ProcessDataGrid?.SelectedItem is ProcessInfo selectedProcess)
        {
            Close(selectedProcess.Id);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}

public class ProcessInfo
{
    public int Id { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string MainWindowTitle { get; set; } = string.Empty;
    public string ProcessType { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
