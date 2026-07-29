using System;
using System.Windows;

namespace DesktopClock;

public partial class ReminderDialog : Window
{
    public ReminderItem Reminder { get; private set; } = new();

    public ReminderDialog() : this(null)
    {
    }

    public ReminderDialog(ReminderItem? existing)
    {
        InitializeComponent();

        if (existing != null)
        {
            Title = "编辑提醒";
            Reminder = new ReminderItem
            {
                Id = existing.Id,
                Title = existing.Title,
                Description = existing.Description,
                DateTime = existing.DateTime,
                DayOfWeek = existing.DayOfWeek,
                TimeOfDay = existing.TimeOfDay,
                IsRecurring = existing.IsRecurring,
                IsEnabled = existing.IsEnabled
            };

            TitleBox.Text = existing.Title;
            DescBox.Text = existing.Description;
            RecurringCheck.IsChecked = existing.IsRecurring;

            if (existing.IsRecurring)
            {
                ReminderDatePicker.SelectedDate = null;
                SelectDayOfWeek(existing.DayOfWeek);
                RecurringTimeBox.Text = existing.TimeOfDay.ToString(@"hh\:mm");
            }
            else
            {
                ReminderDatePicker.SelectedDate = existing.DateTime?.Date;
                ReminderTimeBox.Text = existing.DateTime?.ToString("HH:mm") ?? "09:00";
            }
        }
        else
        {
            ReminderDatePicker.SelectedDate = DateTime.Today;
        }

        UpdatePanels();
    }

    private void SelectDayOfWeek(DayOfWeek? day)
    {
        foreach (var item in DayOfWeekCombo.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem ci && ci.Tag?.ToString() == day?.ToString())
            {
                DayOfWeekCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void RecurringCheck_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePanels();
    }

    private void UpdatePanels()
    {
        bool rec = RecurringCheck.IsChecked == true;
        OncePanel.Visibility = rec ? Visibility.Collapsed : Visibility.Visible;
        RecurringPanel.Visibility = rec ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("请输入提醒标题", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Reminder.Title = TitleBox.Text.Trim();
        Reminder.Description = DescBox.Text.Trim();
        Reminder.IsRecurring = RecurringCheck.IsChecked == true;

        if (Reminder.IsRecurring)
        {
            var dayTag = (DayOfWeekCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();
            if (dayTag != null && Enum.TryParse<DayOfWeek>(dayTag, out var day))
                Reminder.DayOfWeek = day;
            if (TimeSpan.TryParse(RecurringTimeBox.Text, out var rt))
                Reminder.TimeOfDay = rt;
            Reminder.DateTime = null;
        }
        else
        {
            var date = ReminderDatePicker.SelectedDate;
            var timeStr = ReminderTimeBox.Text;
            if (date == null || !TimeSpan.TryParse(timeStr, out var ts))
            {
                MessageBox.Show("请输入有效日期和时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Reminder.DateTime = date.Value.Add(ts);
            Reminder.DayOfWeek = null;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
