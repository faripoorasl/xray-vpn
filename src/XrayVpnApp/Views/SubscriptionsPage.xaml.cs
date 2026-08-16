using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using XrayVpnApp.Models;
using XrayVpnApp.ViewModels;

namespace XrayVpnApp.Views;

public partial class SubscriptionsPage : UserControl
{
    private readonly MainViewModel _vm;
    public ObservableCollection<SubscriptionSource> Subscriptions { get; } = new();

    public SubscriptionsPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Subscriptions.Clear();
        foreach (var s in App.Settings.Store.Subscriptions) Subscriptions.Add(s);
        SubsGrid.ItemsSource = Subscriptions;
    }

    private void AddSubscription_Click(object sender, RoutedEventArgs e)
    {
        var name = SubNameBox.Text.Trim();
        var url = SubUrlBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        var sub = new SubscriptionSource
        {
            Name = string.IsNullOrEmpty(name) ? url : name,
            Url = url,
            AutoUpdate = AutoUpdateBox.IsChecked == true,
            UpdateIntervalHours = int.TryParse(IntervalBox.Text, out var h) ? h : 24,
        };

        App.Settings.Store.Subscriptions.Add(sub);
        Subscriptions.Add(sub);
        App.Settings.SaveStore();

        SubNameBox.Text = "";
        SubUrlBox.Text = "";

        // Auto-fetch
        _ = UpdateSubscriptionAsync(sub);
    }

    private void UpdateSub_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var sub = App.Settings.Store.Subscriptions.FirstOrDefault(s => s.Id == id);
            if (sub != null) _ = UpdateSubscriptionAsync(sub);
        }
    }

    private void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var sub in App.Settings.Store.Subscriptions.ToList())
            _ = UpdateSubscriptionAsync(sub);
    }

    private void DeleteSub_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var sub = App.Settings.Store.Subscriptions.FirstOrDefault(s => s.Id == id);
            if (sub == null) return;
            var msg = (string)Application.Current.FindResource("MsgDeleteConfirm");
            if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            // Remove all servers from this subscription
            var toRemove = App.Settings.Store.Servers
                .Where(s => s.SubscriptionId == sub.Id).ToList();
            foreach (var s in toRemove) App.Settings.Store.Servers.Remove(s);

            App.Settings.Store.Subscriptions.Remove(sub);
            var inView = Subscriptions.FirstOrDefault(s => s.Id == sub.Id);
            if (inView != null) Subscriptions.Remove(inView);

            App.Settings.SaveStore();
        }
    }

    private async Task UpdateSubscriptionAsync(SubscriptionSource sub)
    {
        var (success, message, servers) = await App.Subscription.FetchAsync(sub.Url, sub.Id);

        if (success)
        {
            // Remove old servers from this subscription
            var old = App.Settings.Store.Servers.Where(s => s.SubscriptionId == sub.Id).ToList();
            foreach (var s in old) App.Settings.Store.Servers.Remove(s);

            // Add new
            foreach (var s in servers)
            {
                if (string.IsNullOrEmpty(s.Remark))
                    s.Remark = $"{sub.Name} - {servers.IndexOf(s) + 1}";
                App.Settings.Store.Servers.Add(s);
            }

            sub.LastUpdated = DateTime.Now;
            sub.ServerCount = servers.Count;
            App.Settings.SaveStore();

            // Refresh DataGrid
            Dispatcher.Invoke(() =>
            {
                var item = Subscriptions.FirstOrDefault(s => s.Id == sub.Id);
                if (item != null)
                {
                    item.LastUpdated = sub.LastUpdated;
                    item.ServerCount = sub.ServerCount;
                    SubsGrid.Items.Refresh();
                }
            });

            MessageBox.Show(
                (string)Application.Current.FindResource("MsgSubscriptionUpdated"),
                "✓", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                (string)Application.Current.FindResource("MsgSubscriptionFailed") + "\n" + message,
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
