using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Navigation; // Added for RequestNavigateEventArgs
using OSDeploymentAssistant.Integrations; // ServiceNow integration
using System.Windows;


namespace OSDeploymentAssistant
{
    public class TrackedTicket
    {
        public string TicketID { get; set; } = string.Empty;
        public string AssetName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int MinutesElapsed { get; set; }
        public string TimeRemaining => $"{45 - MinutesElapsed} min left";
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<TrackedTicket> MonitoredTickets { get; set; } = new();

        private DispatcherTimer monitorTimer = new();
        private Random rnd = new();
        private const int MAX_NAME_LENGTH = 14;
        private bool isRemotePCReachable = false;

        // === ServiceNow integration config ===
        // Base URL of your ServiceNow instance.
        private static readonly ServiceNowClient _serviceNowClient = new ServiceNowClient("https://leoni.service-now.com");
        // TODO: replace with the sys_id of your real "SCCM Asset" catalog item in sc_cat_item.
        private const string SccmCatalogItemSysId = "PUT_YOUR_CATALOG_ITEM_SYS_ID_HERE";

        public MainWindow()
        {
            InitializeComponent();
            ListTickets.ItemsSource = MonitoredTickets;
            GrabLocalMacAddress();
            UpdateUserAssignment();
            TriggerInitialOSSetup();
            SetupMonitoringTimer();
            
            // Initialize the Execute button state
            UpdateExecuteButtonState();
        }

        private void SetupMonitoringTimer()
        {
            monitorTimer.Interval = TimeSpan.FromMinutes(1);
            monitorTimer.Tick += MonitorTimer_Tick;
            monitorTimer.Start();
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            var itemsToRemove = MonitoredTickets.Where(t => t.MinutesElapsed >= 45).ToList();
            foreach (var item in itemsToRemove)
            {
                ShowTicketClosedNotification(item);
                MonitoredTickets.Remove(item);
            }

            foreach (var ticket in MonitoredTickets)
            {
                ticket.MinutesElapsed++;
                
                if (ticket.MinutesElapsed == 45)
                {
                    ticket.Status = "⚠️ Expiring - Auto-close in 1 min";
                }
                else if (ticket.MinutesElapsed == 44)
                {
                    ticket.Status = "⏳ Expiring soon - 1 minute remaining";
                }
                else if (ticket.MinutesElapsed == 2 && ticket.Status == "Staging (OS-Install)")
                {
                    ticket.Status = "Ready (Run IPv4)";
                    TriggerSystemAlertNotification(ticket);
                }
            }
            ListTickets.Items.Refresh();
        }

        private void ShowTicketClosedNotification(TrackedTicket ticket)
        {
            if (ToastText == null || ToastPopup == null) return;

            ToastText.Text = $"🔔 TICKET CLOSED: {ticket.TicketID}\n\n" +
                             $"Asset: {ticket.AssetName}\n" +
                             $"Status: Completed & Closed\n" +
                             $"Duration: 45 minutes (maximum lifecycle reached)\n\n" +
                             $"The ticket has been automatically closed and removed from the active list.";
            
            ToastPopup.IsOpen = true;
            
            MessageBox.Show($"Ticket {ticket.TicketID} for asset {ticket.AssetName} has been automatically closed.\n\n" +
                            "Reason: Maximum lifecycle of 45 minutes reached.\n" +
                            "The ticket has been removed from the active monitoring list.",
                            "Ticket Auto-Closed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TriggerSystemAlertNotification(TrackedTicket ticket)
        {
            if (ToastText == null || ToastPopup == null) return;

            ToastText.Text = $"✅ ASSET BUILD COMPLETED: {ticket.AssetName}\n\n" +
                             $"Ticket: {ticket.TicketID}\n" +
                             $"Status: Ready for deployment\n\n" +
                             $"System is online. Ready to deploy specialized firewall rules and target network architecture presets.\n\n" +
                             $"⚠️ Ticket will auto-close in 43 minutes.";
            ToastPopup.IsOpen = true;
        }
       
        private void GrabLocalMacAddress()
        {
            if (TxtMac == null) return;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet && nic.OperationalStatus == OperationalStatus.Up)
                {
                    TxtMac.Text = nic.GetPhysicalAddress().ToString();
                    break;
                }
            }
            if (string.IsNullOrEmpty(TxtMac.Text)) TxtMac.Text = "00AA11BB22CC";
        }

        private void TriggerInitialOSSetup() 
        { 
            OnOSSelectionChanged(ComboOS, new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, new ArrayList(), new ArrayList())); 
        }

        private void OnNodeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUserAssignment();
            UpdateFormStateEngine();
        }

        private void UpdateUserAssignment()
        {
            if (ComboNode == null || TxtUser == null) return;
            string nodeText = (ComboNode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "LTN";
            TxtUser.Text = nodeText == "LTN" ? "Jasser Ben Abdallah" : "Mohamed ElHadhri";
        }

        private void OnOSSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboOS == null || ComboType == null || PanelUser == null || PanelRollout == null || PanelPatch == null) return;
            if (ComboOS.SelectedItem is ComboBoxItem selectedOS)
            {
                string osText = selectedOS.Content?.ToString() ?? "";
                ComboType.Items.Clear();

                if (osText.Contains("LTSC"))
                {
                    ComboType.Items.Add(new ComboBoxItem { Content = "PD", IsSelected = true });
                    ComboType.Items.Add(new ComboBoxItem { Content = "KM (Komax PC)" });
                    ComboType.Items.Add(new ComboBoxItem { Content = "TB" });
                    PanelUser.Visibility = Visibility.Collapsed;
                    PanelRollout.Visibility = Visibility.Collapsed;
                    PanelPatch.Visibility = Visibility.Visible;
                }
                else
                {
                    ComboType.Items.Add(new ComboBoxItem { Content = "Workstation (WS)", IsSelected = true });
                    ComboType.Items.Add(new ComboBoxItem { Content = "Notebook (NB)" });
                    ComboType.Items.Add(new ComboBoxItem { Content = "Tablette (TB)" });
                    PanelUser.Visibility = Visibility.Visible;
                    PanelRollout.Visibility = Visibility.Visible;
                    PanelPatch.Visibility = Visibility.Collapsed;
                }
                UpdateFormStateEngine();
            }
        }

        private void UpdateFormState(object sender, SelectionChangedEventArgs e) 
        { 
            UpdateFormStateEngine(); 
        }

        private void UpdateSuffixTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox txtSuffix && txtSuffix == TxtSuffix)
            {
                string node = (ComboNode?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "LTN";
                string currentOS = (ComboOS?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                string typeRaw = (ComboType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                string prefix = "MD";

                if (currentOS.Contains("LTSC"))
                {
                    if (typeRaw.StartsWith("PD")) prefix = "PD";
                    else if (typeRaw.StartsWith("KM")) prefix = "KM";
                    else if (typeRaw.StartsWith("TB")) prefix = "TB";
                }
                else
                {
                    if (typeRaw.Contains("Notebook")) prefix = "NB";
                    else if (typeRaw.Contains("Workstation")) prefix = "WS";
                    else if (typeRaw.Contains("Tablette") || typeRaw.Contains("TB")) prefix = "TB";
                }

                int maxSuffixLength = 11 - (prefix.Length + node.Length);
                if (maxSuffixLength < 0) maxSuffixLength = 0;
                
                if (SuffixCharCounter != null)
                {
                    SuffixCharCounter.Text = $"{txtSuffix.Text.Length} / {maxSuffixLength}";
                    
                    if (txtSuffix.Text.Length >= maxSuffixLength)
                    {
                        SuffixCharCounter.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    else if (txtSuffix.Text.Length >= maxSuffixLength * 0.8)
                    {
                        SuffixCharCounter.Foreground = System.Windows.Media.Brushes.Orange;
                    }
                    else
                    {
                        SuffixCharCounter.Foreground = System.Windows.Media.Brushes.Gray;
                    }
                }
                
                if (txtSuffix.Text.Length > maxSuffixLength)
                {
                    TxtSuffix.TextChanged -= UpdateSuffixTextChanged;
                    txtSuffix.Text = txtSuffix.Text.Substring(0, maxSuffixLength);
                    txtSuffix.SelectionStart = txtSuffix.Text.Length;
                    TxtSuffix.TextChanged += UpdateSuffixTextChanged;
                    
                    txtSuffix.Background = System.Windows.Media.Brushes.LightYellow;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
                    timer.Tick += (s, args) => 
                    {
                        txtSuffix.Background = System.Windows.Media.Brushes.White;
                        timer.Stop();
                    };
                    timer.Start();
                }
                else
                {
                    txtSuffix.Background = System.Windows.Media.Brushes.White;
                }
            }
            
            UpdateFormStateEngine();
        }

        private void UpdateFormStateEngine()
        {
            if (ComboNode == null || ComboType == null || TxtPreview == null || ComboOS == null) return;
            string node = (ComboNode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "LTN";
            string suffix = TxtSuffix?.Text ?? "";
            string currentOS = (ComboOS.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string typeRaw = (ComboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string prefix = "MD";

            if (currentOS.Contains("LTSC"))
            {
                if (typeRaw.StartsWith("PD")) prefix = "PD";
                else if (typeRaw.StartsWith("KM")) prefix = "KM";
                else if (typeRaw.StartsWith("TB")) prefix = "TB";
            }
            else
            {
                if (typeRaw.Contains("Notebook")) prefix = "NB";
                else if (typeRaw.Contains("Workstation")) prefix = "WS";
                else if (typeRaw.Contains("Tablette") || typeRaw.Contains("TB")) prefix = "TB";
            }
            
            int maxSuffixLength = 11 - (prefix.Length + node.Length);
            if (maxSuffixLength < 0) maxSuffixLength = 0;
            
            string baseName = $"{prefix}{node}{suffix}".ToUpper();
            
            if (baseName.Length > 11)
            {
                baseName = baseName.Substring(0, 11);
                
                if (TxtSuffix != null)
                {
                    TxtSuffix.TextChanged -= UpdateSuffixTextChanged;
                    string currentSuffix = TxtSuffix.Text ?? "";
                    string newSuffix = baseName.Substring(prefix.Length + node.Length);
                    if (currentSuffix != newSuffix)
                    {
                        TxtSuffix.Text = newSuffix;
                        TxtSuffix.SelectionStart = TxtSuffix.Text.Length;
                    }
                    TxtSuffix.TextChanged += UpdateSuffixTextChanged;
                }
            }
            
            if (SuffixCharCounter != null && TxtSuffix != null)
            {
                SuffixCharCounter.Text = $"{TxtSuffix.Text.Length} / {maxSuffixLength}";
                
                if (TxtSuffix.Text.Length >= maxSuffixLength)
                {
                    SuffixCharCounter.Foreground = System.Windows.Media.Brushes.Red;
                }
                else if (TxtSuffix.Text.Length >= maxSuffixLength * 0.8)
                {
                    SuffixCharCounter.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    SuffixCharCounter.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }
            
            TxtPreview.Text = $"{baseName}■■■";
        }

        private async void CreateServiceNowTicketClick(object sender, RoutedEventArgs e)
        {
            if (TxtMac == null || TxtPreview == null) return;

            string[] macLines = TxtMac.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (macLines.Length == 0)
            {
                MessageBox.Show("Please insert at least one valid MAC address to proceed.", "Input Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var invalidMacs = new System.Collections.Generic.List<string>();
            foreach (string line in macLines)
            {
                string mac = line.Trim();
                string cleanMac = new string(mac.Where(c => char.IsDigit(c) || (c >= 'A' && c <= 'F')).ToArray());
                if (cleanMac.Length != 12)
                {
                    invalidMacs.Add(mac);
                }
            }

            if (invalidMacs.Any())
            {
                string invalidList = string.Join(Environment.NewLine, invalidMacs.Take(5));
                string message = $"The following MAC addresses are invalid (must be exactly 12 hex characters):{Environment.NewLine}{invalidList}";
                if (invalidMacs.Count > 5) message += $"{Environment.NewLine}... and {invalidMacs.Count - 5} more";
                MessageBox.Show(message, "Invalid MAC Addresses", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string basePrefix = TxtPreview.Text.Replace("■", "").Trim();
            if (basePrefix.Length > 11)
            {
                basePrefix = basePrefix.Substring(0, 11);
            }

            this.IsEnabled = false;

            int registeredCount = 0;
            var failures = new System.Collections.Generic.List<string>();
            string requestedFor = TxtUser?.Text ?? Environment.UserName;
            string node = (ComboNode?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string osName = (ComboOS?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            foreach (string line in macLines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                string numericSuffix = rnd.Next(100, 999).ToString();
                string uniqueName = $"{basePrefix}{numericSuffix}";

                if (uniqueName.Length > MAX_NAME_LENGTH)
                {
                    int prefixLength = MAX_NAME_LENGTH - numericSuffix.Length;
                    uniqueName = prefixLength > 0
                        ? $"{basePrefix.Substring(0, prefixLength)}{numericSuffix}"
                        : numericSuffix;
                }

                var requestItem = new ServiceNowRequestItem
                {
                    RequestedFor = requestedFor,
                    ShortDescription = $"SCCM asset provisioning for {uniqueName}",
                    CatalogItemSysId = SccmCatalogItemSysId,
                    AssetName = uniqueName,
                    MacAddress = trimmedLine,
                    Node = node,
                    OperatingSystem = osName
                };

                try
                {
                    ServiceNowTicketResult ticket = await _serviceNowClient.CreateSccmAssetRequestAsync(requestItem);

                    if (string.IsNullOrWhiteSpace(ticket.Number))
                    {
                        // ServiceNow didn't return a real RITMxxxxxxx number - treat as a failure
                        // rather than showing the internal sys_id as if it were the ticket name.
                        throw new ServiceNowException(
                            $"ServiceNow accepted the record (sys_id {ticket.SysId}) but did not return a RITM number.");
                    }

                    MonitoredTickets.Add(new TrackedTicket
                    {
                        TicketID = ticket.Number, // e.g. RITM0012345
                        AssetName = uniqueName,
                        Status = "Staging (OS-Install)",
                        MinutesElapsed = 0
                    });
                    registeredCount++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{trimmedLine} ({uniqueName}): {ex.Message}");
                }
            }

            this.IsEnabled = true;

            if (failures.Any())
            {
                string failureList = string.Join(Environment.NewLine, failures.Take(5));
                string extra = failures.Count > 5 ? $"{Environment.NewLine}... and {failures.Count - 5} more" : "";
                MessageBox.Show(
                    $"Created {registeredCount} ServiceNow ticket(s).\n\n{failures.Count} request(s) failed:\n{failureList}{extra}",
                    "Partial Failure", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    $"Successfully created {registeredCount} ServiceNow ticket(s) for SCCM asset provisioning!\r\nAll tickets queued inside the active monitoring view.",
                    "Batch Provision Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void TxtMac_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtMac == null) return;

            TxtMac.TextChanged -= TxtMac_TextChanged;
            
            try
            {
                int caretIndex = TxtMac.SelectionStart;
                string originalText = TxtMac.Text;
                
                string[] lines = originalText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var resultLines = new System.Collections.Generic.List<string>();

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        resultLines.Add("");
                        continue;
                    }

                    string clean = "";
                    foreach (char c in line.ToUpper())
                    {
                        if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
                        {
                            clean += c;
                        }
                    }

                    if (clean.Length > 12)
                    {
                        clean = clean.Substring(0, 12);
                    }

                    string formatted = "";
                    for (int i = 0; i < clean.Length; i++)
                    {
                        if (i > 0 && i % 2 == 0)
                        {
                            formatted += ":";
                        }
                        formatted += clean[i];
                    }

                    if (clean.Length > 0 && clean.Length % 2 == 0 && clean.Length < 12)
                    {
                        formatted += ":";
                    }

                    resultLines.Add(formatted);
                }

                string newText = string.Join(Environment.NewLine, resultLines);

                if (newText != originalText)
                {
                    TxtMac.Text = newText;
                    TxtMac.SelectionStart = Math.Min(caretIndex, newText.Length);
                }
            }
            finally
            {
                TxtMac.TextChanged += TxtMac_TextChanged;
            }
        }

        private void RdoLocal_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelRemote != null && TxtRemotePC != null)
            {
                PanelRemote.IsEnabled = false;
                TxtRemotePC.IsEnabled = false;
                TxtRemotePC.Background = System.Windows.Media.Brushes.LightGray;
                TxtRemotePC.Text = "";
                
                if (BtnPing != null) BtnPing.IsEnabled = false;
                ResetPingStatus();
                isRemotePCReachable = false;
                UpdateExecuteButtonState();
            }
            UpdateAutomationStatus("Local machine selected");
        }

        private void RdoRemote_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelRemote != null && TxtRemotePC != null)
            {
                PanelRemote.IsEnabled = true;
                TxtRemotePC.IsEnabled = true;
                TxtRemotePC.Background = System.Windows.Media.Brushes.White;
                TxtRemotePC.Focus();
                
                if (BtnPing != null) BtnPing.IsEnabled = true;
                ResetPingStatus();
                isRemotePCReachable = false;
                UpdateExecuteButtonState();
            }
            UpdateAutomationStatus("Remote PC selected - Enter computer name and test connectivity");
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtRemotePC?.Text))
            {
                MessageBox.Show("Please enter a computer name first.", "Missing Information", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string computerName = TxtRemotePC.Text.Trim();
            UpdatePingStatus("Testing...", "#F59E0B", false);
            if (BtnPing != null)
            {
                BtnPing.IsEnabled = false;
                BtnPing.Content = "⏳ Testing...";
            }

            UpdateExecuteButtonState();

            try
            {
                bool isReachable = await PingComputerAsync(computerName);
                
                if (isReachable)
                {
                    isRemotePCReachable = true;
                    UpdatePingStatus($"✅ Online - {computerName} is reachable", "#10B981", true);
                    UpdateAutomationStatus($"✓ {computerName} is online and reachable");
                    MessageBox.Show($"✅ {computerName} is online and reachable!", "Ping Successful", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    isRemotePCReachable = false;
                    UpdatePingStatus($"❌ Offline - {computerName} is not reachable", "#EF4444", false);
                    UpdateAutomationStatus($"✗ {computerName} is offline or not accessible");
                    
                    MessageBox.Show($"Computer '{computerName}' is not reachable.\n\nPlease check:\n- Computer name is correct\n- Computer is powered on\n- Network connectivity\n- Firewall is not blocking ICMP", 
                        "Ping Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                isRemotePCReachable = false;
                UpdatePingStatus($"⚠️ Error: {ex.Message}", "#EF4444", false);
                UpdateAutomationStatus($"✗ Ping error: {ex.Message}");
            }
            finally
            {
                if (BtnPing != null)
                {
                    BtnPing.IsEnabled = true;
                    BtnPing.Content = "🔍 Ping";
                }
                UpdateExecuteButtonState();
            }
        }

        private void UpdateExecuteButtonState()
        {
            if (BtnExecute != null)
            {
                if (RdoLocal != null && RdoLocal.IsChecked == true)
                {
                    BtnExecute.IsEnabled = true;
                    return;
                }
                
                if (RdoRemote != null && RdoRemote.IsChecked == true)
                {
                    BtnExecute.IsEnabled = isRemotePCReachable;
                    
                    if (string.IsNullOrWhiteSpace(TxtRemotePC?.Text))
                    {
                        BtnExecute.IsEnabled = false;
                    }
                }
            }
        }

        private async Task<bool> PingComputerAsync(string computerName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (Ping ping = new Ping())
                    {
                        PingReply? reply = ping.Send(computerName, 3000); 
                        return reply != null && reply.Status == IPStatus.Success;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        private void TxtRemotePC_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && BtnPing != null && BtnPing.IsEnabled)
            {
                BtnPing_Click(sender, e);
            }
        }

        private void UpdatePingStatus(string statusText, string colorHex, bool isSuccess)
        {
            if (PingStatusText != null)
            {
                PingStatusText.Text = statusText;
                PingStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
            }
            
            if (PingIndicator != null)
            {
                PingIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
            }
            
            if (PingStatus != null)
            {
                if (isSuccess)
                {
                    PingStatus.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                    PingStatus.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECFDF5"));
                }
                else
                {
                    PingStatus.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0"));
                    PingStatus.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F1F5F9"));
                }
            }
        }

        private void ResetPingStatus()
        {
            UpdatePingStatus("Not tested", "#94A3B8", false);
        }

        private void UpdateAutomationStatus(string message)
        {
            if (TxtAutomationStatus != null)
            {
                TxtAutomationStatus.Text = message;
            }
        }

        public async void RunAutomationClick(object sender, RoutedEventArgs e)
        {
            if (ToastPopup != null) ToastPopup.IsOpen = false;

            if (RdoRemote != null && RdoRemote.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(TxtRemotePC?.Text))
                {
                    MessageBox.Show("Please enter the remote computer name.", "Missing Information", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!isRemotePCReachable)
                {
                    var result = MessageBox.Show("The remote PC has not been tested or is not reachable.\n\nDo you want to try pinging it now?",
                        "Connection Not Verified", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        BtnPing_Click(sender, e);
                        await Task.Delay(1000); 
                        
                        if (!isRemotePCReachable)
                        {
                            MessageBox.Show("Cannot proceed - remote PC is not reachable.", 
                                "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        var confirmResult = MessageBox.Show("Are you sure you want to proceed without verifying connectivity?\n\nRemote execution may fail if the computer is not reachable.",
                            "Proceed Without Ping?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        
                        if (confirmResult == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                }
            }

            string psScript = BuildPowerShellScript();

            if (string.IsNullOrWhiteSpace(psScript))
            {
                MessageBox.Show("Please select at least one automation option.", "No Options Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (RdoRemote != null && RdoRemote.IsChecked == true)
            {
                string remotePC = TxtRemotePC?.Text.Trim() ?? "";
                await ExecuteRemoteAutomation(remotePC, psScript);
            }
            else
            {
                await ExecuteLocalAutomation(psScript);
            }
        }

        private string BuildPowerShellScript()
        {
            string psScript = "";

            if (ChkFirewall?.IsChecked == true)
            {
                psScript += @"
                    New-NetFirewallRule -DisplayName 'Allow Inbound ICMPv4' -Direction Inbound -Protocol ICMPv4 -IcmpType 8 -Action Allow -ErrorAction SilentlyContinue;
                ";
            }

            if (ChkTimezone?.IsChecked == true)
            {
                psScript += @"
                    Set-TimeZone -Id 'W. Central Africa Standard Time' -ErrorAction SilentlyContinue;
                ";
            }

            if (ChkKeyboard?.IsChecked == true)
            {
                psScript += @"
                    Set-WinUserLanguageList -LanguageList fr-FR -Force;
                ";
            }

            if (ChkRegistryFix?.IsChecked == true)
            {
                psScript += @"
                    $p = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'; 
                    if (!(Test-Path $p)) { New-Item $p -Force }; 
                    Set-ItemProperty $p -Name 'DoNotConnectToWindowsUpdateInternetLocations' -Value 0 -ErrorAction SilentlyContinue;
                ";
            }

            if (ChkSCCM?.IsChecked == true)
            {
                psScript += @"
                    try {
                        Invoke-WmiMethod -Namespace root\ccm -Class sms_client -Name TriggerSchedule '{00000000-0000-0000-0000-000000000021}' -ErrorAction SilentlyContinue;
                        Invoke-WmiMethod -Namespace root\ccm -Class sms_client -Name TriggerSchedule '{00000000-0000-0000-0000-000000000022}' -ErrorAction SilentlyContinue;
                        Invoke-WmiMethod -Namespace root\ccm -Class sms_client -Name TriggerSchedule '{00000000-0000-0000-0000-000000000023}' -ErrorAction SilentlyContinue;
                        Write-Host 'SCCM policies triggered successfully';
                    } catch {
                        Write-Host 'SCCM client not found or not installed';
                    }
                ";
            }

            if (ChkAD?.IsChecked == true)
            {
                psScript += @"
                    try {
                        $computerName = $env:COMPUTERNAME;
                        $description = 'Managed by OS Deployment Assistant - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm');
                        Import-Module ActiveDirectory -ErrorAction Stop;
                        Set-ADComputer -Identity $computerName -Description $description -ErrorAction Stop;
                        Write-Host 'AD description updated successfully';
                    } catch {
                        Write-Host 'AD update failed: ' + $_.Exception.Message;
                    }
                ";
            }

            return psScript;
        }

        private async Task ExecuteLocalAutomation(string psScript)
        {
            UpdateAutomationStatus("Executing on local machine...");
            
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    UpdateAutomationStatus("✓ Completed on local machine");
                    ShowSuccessMessage("local machine");
                }
            }
            catch (Exception ex)
            {
                UpdateAutomationStatus($"✗ Error: {ex.Message}");
                MessageBox.Show($"Local execution failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteRemoteAutomation(string remotePC, string psScript)
        {
            UpdateAutomationStatus($"Connecting to remote PC: {remotePC}...");

            try
            {
                string remoteScript = $@"
                    try {{
                        if (!(Test-Connection -ComputerName '{remotePC}' -Count 1 -Quiet)) {{
                            Write-Error 'Computer is not reachable via ping';
                            exit 1;
                        }}

                        $session = New-PSSession -ComputerName '{remotePC}' -ErrorAction Stop;
                        $result = Invoke-Command -Session $session -ScriptBlock {{
                            {psScript}
                        }};
                        Remove-PSSession $session;
                        Write-Host 'Remote execution completed successfully';
                    }} catch {{
                        Write-Error 'Remote execution failed: ' + $_.Exception.Message;
                        exit 1;
                    }}
                ";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{remoteScript}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        UpdateAutomationStatus($"✓ Completed on remote PC: {remotePC}");
                        ShowSuccessMessage($"remote PC: {remotePC}");
                    }
                    else
                    {
                        UpdateAutomationStatus($"✗ Failed on remote PC: {remotePC}");
                        MessageBox.Show($"Remote execution failed on {remotePC}.\nPlease check:\n- WinRM is enabled\n- Firewall allows remote management\n- You have admin permissions", 
                            "Remote Execution Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateAutomationStatus($"✗ Error: {ex.Message}");
                MessageBox.Show($"Remote execution failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSuccessMessage(string target)
        {
            string message = $"Automation completed successfully on {target}!\n\nTasks completed:";
            
            if (ChkFirewall?.IsChecked == true) message += "\n✅ Firewall rule applied (Ping allowed)";
            if (ChkTimezone?.IsChecked == true) message += "\n✅ Timezone set to W. Central Africa Standard Time";
            if (ChkKeyboard?.IsChecked == true) message += "\n✅ Keyboard layout set to French (fr-FR)";
            if (ChkRegistryFix?.IsChecked == true) message += "\n✅ Windows Update registry fix applied";
            if (ChkSCCM?.IsChecked == true) message += "\n✅ SCCM client policy retrieval triggered";
            if (ChkAD?.IsChecked == true) message += "\n✅ AD computer description updated";

            MessageBox.Show(message, "Automation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // FIXED: Added the missing Hyperlink RequestNavigate handler
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start("OUTLOOK.EXE");
                    MessageBox.Show("Outlook opened. Please compose a new email to jasser.ben-abdallah@leoni.com", 
                        "Outlook Launched", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    Clipboard.SetText("jasser.ben-abdallah@leoni.com");
                    MessageBox.Show("Email address copied to clipboard!", "Contact Info", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            e.Handled = true;
        }
    }
}