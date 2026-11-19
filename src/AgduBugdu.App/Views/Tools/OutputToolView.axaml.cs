using System;
using Avalonia;
using Avalonia.Controls;

namespace AgduBugdu.App.Views.Tools;

public partial class OutputToolView : UserControl
{
    public OutputToolView()
    {
        InitializeComponent();

        var textBox = this.FindControl<TextBox>("OutputTextBox");
        if (textBox != null)
        {
            textBox.GetObservable(TextBox.TextProperty).Subscribe(text =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    textBox.CaretIndex = text.Length;
                }
            });
        }
    }
}
