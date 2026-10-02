using System;
using System.IO;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace AgduBugdu.App.ViewModels.Documents;

public partial class EditorDocumentViewModel : Document
{
    private string _originalText = string.Empty;

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
        _originalText = TextDocument.Text;
    }

    partial void OnLineChanged(int value)
    {
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    partial void OnColumnChanged(int value)
    {
        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    public void CheckModified()
    {
        bool modified = TextDocument.Text != _originalText;
        if (IsModified != modified)
        {
            IsModified = modified;
            Title = modified ? $"{FileName}*" : FileName;
        }
    }

    public static EditorDocumentViewModel FromFile(string path)
    {
        var content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var textDoc = new TextDocument(content);
        var vm = new EditorDocumentViewModel
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            Id = path,
            TextDocument = textDoc,
            IsModified = false
        };
        vm._originalText = textDoc.Text;
        vm.Title = vm.FileName;
        return vm;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(FilePath))
            return;

        File.WriteAllText(FilePath, TextDocument.Text);
        _originalText = TextDocument.Text;
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

