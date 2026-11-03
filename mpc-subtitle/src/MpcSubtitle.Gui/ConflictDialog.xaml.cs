using System.Windows;
using MpcSubtitle.Core;

namespace MpcSubtitle.Gui;

public partial class ConflictDialog : Window
{
    public ConflictDecision Decision { get; private set; } = ConflictDecision.Skip;

    public ConflictDialog(string videoPath, string srtPath)
    {
        InitializeComponent();

        MessageText.Text =
            $"A subtitle already exists for this video:\n\n{videoPath}\n\nSubtitle file:\n{srtPath}\n\nChoose an action:";

        SkipButton.Click += (_, _) => CloseWith(ConflictDecision.Skip);
        SkipAllButton.Click += (_, _) => CloseWith(ConflictDecision.SkipAll);
        ReplaceButton.Click += (_, _) => CloseWith(ConflictDecision.Replace);
        ReplaceAllButton.Click += (_, _) => CloseWith(ConflictDecision.ReplaceAll);
    }

    private void CloseWith(ConflictDecision decision)
    {
        Decision = decision;
        DialogResult = true;
    }
}
