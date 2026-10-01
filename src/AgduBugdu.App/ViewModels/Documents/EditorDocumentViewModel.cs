using System;
using System.IO;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Documents;

public partial class EditorDocumentViewModel : Document
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = "Untitled";

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private TextDocument _textDocument = new();

    [ObservableProperty]
    private int _line = 1;

    [ObservableProperty]
    private int _column = 1;

    [ObservableProperty]
    private bool _wordWrap = false;

    public event EventHandler? CaretMoved;

    public EditorDocumentViewModel()
    {
        Id = Guid.NewGuid().ToString();
        Title = FileName;
    }

    partial void OnLineChanged(int value)
    {
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    partial void OnColumnChanged(int value)
    {
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    public static EditorDocumentViewModel FromFile(string path)
    {
        var vm = new EditorDocumentViewModel
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            Id = path
        };
        vm.Title = vm.FileName;

        if (File.Exists(path))
        {
            var content = File.ReadAllText(path);
            vm.TextDocument = new TextDocument(content);
        }

        return vm;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(FilePath))
            return;

        File.WriteAllText(FilePath, TextDocument.Text);
        IsModified = false;
        Title = FileName;
    }

    public void SaveAs(string newPath)
    {
        FilePath = newPath;
        FileName = Path.GetFileName(newPath);
        Id = newPath;
        Save();
    }
}
