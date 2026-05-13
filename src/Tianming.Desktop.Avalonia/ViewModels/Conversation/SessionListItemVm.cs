using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public partial class SessionListItemVm : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private DateTime _updatedAt;
    [ObservableProperty] private int _messageCount;
}
