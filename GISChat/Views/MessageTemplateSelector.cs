using System.Windows;
using System.Windows.Controls;
using GISChat.Models;

namespace GISChat.Views;

/// <summary>
/// Selects the correct DataTemplate for chat messages based on their role.
/// </summary>
public class MessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? AssistantTemplate { get; set; }
    public DataTemplate? SystemTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is ChatMessage msg)
        {
            return msg.Role switch
            {
                MessageRole.User => UserTemplate,
                MessageRole.Assistant => AssistantTemplate,
                MessageRole.System => SystemTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }
        return base.SelectTemplate(item, container);
    }
}
