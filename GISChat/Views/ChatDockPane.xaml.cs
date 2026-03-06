using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GISChat.Models;

namespace GISChat.Views;

public partial class ChatDockPane : UserControl
{
    public ChatDockPane()
    {
        InitializeComponent();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            if (DataContext is ChatDockPaneViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
        }
    }

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChatMessage msg)
        {
            var parts = new System.Text.StringBuilder();
            parts.AppendLine(msg.Text);
            if (msg.HasCode)
            {
                parts.AppendLine();
                parts.AppendLine(msg.CodeBlock);
            }
            if (msg.HasResult)
            {
                parts.AppendLine();
                parts.AppendLine(msg.ExecutionResult);
            }
            Clipboard.SetText(parts.ToString().TrimEnd());
            btn.Content = "Copied!";
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (_, _) => { btn.Content = "Copy"; timer.Stop(); };
            timer.Start();
        }
    }
}
