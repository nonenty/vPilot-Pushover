﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;

using RossCarlson.Vatsim.Vpilot.Plugins;
using RossCarlson.Vatsim.Vpilot.Plugins.Events;

namespace vPilot_Pushover {
    public class Main : IPlugin {

        public static string version = "1.1.0";

        // Init
        private IBroker vPilot;
        private INotifier notifier;
        private Acars acars;

        public string Name { get; } = "vPilot Pushover";

        // Public variables
        public string connectedCallsign = null;

        // Settings
        private Boolean settingsLoaded = false;
        private IniFile settingsFile;

        private String settingDriver = null;
        private Boolean settingPrivateEnabled = false;
        private Boolean settingRadioEnabled = false;
        private Boolean settingSelcalEnabled = false;
        private Boolean settingHoppieEnabled = false;
        private Boolean settingDisconnectEnabled = false;
        public String settingHoppieLogon = null;
        private String settingPushoverToken = null;
        private String settingPushoverUser = null;
        private String settingPushoverDevice = null;
        private String settingTelegramBotToken = null;
        private String settingTelegramChatId = null;
        private String settingGotifyUrl = null;
        private String settingGotifyToken = null;

        /*
         * 
         * Initilise the plugin
         *
        */
        public void Initialize( IBroker broker ) {
            vPilot = broker;
            loadSettings();

            if (settingsLoaded) {

                // Load the correct notifier driver
                if (settingDriver.ToLower() == "pushover") {
                    notifier = new Drivers.Pushover();

                    NotifierConfig config;
                    config = new NotifierConfig {
                        settingPushoverToken = settingPushoverToken,
                        settingPushoverUser = settingPushoverUser,
                        settingPushoverDevice = settingPushoverDevice
                    };
                    notifier.init(config);
                    if(!notifier.hasValidConfig()) {
                        sendDebug("Pushover API key or user key not set. Check your vPilot-Pushover.ini");
                        return;
                    }

                    sendDebug("Driver set to Pushover");
                } else if (settingDriver.ToLower() == "telegram") {
                    notifier = new Drivers.Telegram();

                    NotifierConfig config;
                    config = new NotifierConfig {
                        settingTelegramBotToken = settingTelegramBotToken,
                        settingTelegramChatId = settingTelegramChatId
                    };
                    notifier.init(config);
                    if (!notifier.hasValidConfig()) {
                        sendDebug("Telegram bot token or chat ID not set. Check your vPilot-Pushover.ini");
                        return;
                    }

                    sendDebug("Driver set to Telegram");
                } else if (settingDriver.ToLower() == "gotify") {
                    notifier = new Drivers.Gotify();

                    NotifierConfig config;
                    config = new NotifierConfig
                    {
                        settingGotifyUrl = settingGotifyUrl,
                        settingGotifyToken = settingGotifyToken
                    };
                    notifier.init(config);
                    if (!notifier.hasValidConfig())
                    {
                        sendDebug("Invalid Gotify server URL or app token. Check your vPilot-Pushover.ini");
                        return;
                    }

                    sendDebug("Driver set to Gotify");
                } else {
                    sendDebug("Driver not set correctly. Check your vPilot-Pushover.ini");
                    return;
                }

                // Subscribe to events according to settings
                vPilot.NetworkConnected += onNetworkConnectedHandler;
                vPilot.NetworkDisconnected += onNetworkDisconnectedHandler;
                if (settingPrivateEnabled) vPilot.PrivateMessageReceived += onPrivateMessageReceivedHandler;
                if (settingRadioEnabled) vPilot.RadioMessageReceived += onRadioMessageReceivedHandler;
                if (settingSelcalEnabled) vPilot.SelcalAlertReceived += onSelcalAlertReceivedHandler;

                // Enable ACARS if Hoppie is enabled
                if (settingHoppieEnabled) {
                    acars = new Acars();
                    acars.init(this, notifier, settingHoppieLogon);
                }

                notifier.sendMessage($"Connected. Running version v{version}");
                sendDebug($"vPilot Pushover connected and enabled on v{version}");

                updateChecker();

            } else {
                sendDebug("vPilot Pushover plugin failed to load. Check your vPilot-Pushover.ini");
            }

        }

        /*
         * 
         * Send debug message to vPilot
         *
        */
        public void sendDebug( String text ) {
            vPilot.PostDebugMessage(text);
        }

        /*
         * 
         * Hook: Network connected
         *
        */
        private void onNetworkConnectedHandler( object sender, NetworkConnectedEventArgs e ) {
            connectedCallsign = e.Callsign;

            if (settingHoppieEnabled) {
                acars.start();
            }
        }
        /*
         * 
         * Hook: Network disconnected
         *
        */
        private void onNetworkDisconnectedHandler( object sender, EventArgs e ) {
            connectedCallsign = null;

            if (settingHoppieEnabled) {
                acars.stop();
            }

            if (settingDisconnectEnabled) {
                notifier.sendMessage("Disconnected from network", "vPilot", 1);
            }
        }

        /*
         * 
         * Hook: Private Message
         *
        */
        private void onPrivateMessageReceivedHandler( object sender, PrivateMessageReceivedEventArgs e ) {
            string from = e.From;
            string message = e.Message;
            notifier.sendMessage(message, from, 1);
        }

        /*
         * 
         * Hook: Radio Message
         *
        */
        private void onRadioMessageReceivedHandler( object sender, RadioMessageReceivedEventArgs e ) {
            string from = e.From;
            string message = e.Message;

            if (message.Contains(connectedCallsign)) {
                notifier.sendMessage(message, from, 1);
            }

        }

        /*
         * 
         * Hook: SELCAL Message
         *
        */
        private void onSelcalAlertReceivedHandler( object sender, SelcalAlertReceivedEventArgs e ) {
            string from = e.From;
            notifier.sendMessage("SELCAL Alert", from, 1);
        }
        

        /*
         * 
         * Load plugin settings
         *
        */
        private void loadSettings() {

            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\vPilot");
            if (registryKey != null) {
                string vPilotPath = (string)registryKey.GetValue("Install_Dir");
                string configFile = vPilotPath + "\\Plugins\\vPilot-Pushover.ini";
                settingsFile = new IniFile(configFile);

                // Set all values
                settingDriver = settingsFile.KeyExists("Driver", "General") ? settingsFile.Read("Driver", "General") : null;
                settingPushoverToken = settingsFile.KeyExists("ApiKey", "Pushover") ? settingsFile.Read("ApiKey", "Pushover") : null;
                settingPushoverUser = settingsFile.KeyExists("UserKey", "Pushover") ? settingsFile.Read("UserKey", "Pushover") : null;
                settingPushoverDevice = settingsFile.KeyExists("Device", "Pushover") ? settingsFile.Read("Device", "Pushover") : null;
                settingHoppieEnabled = settingsFile.KeyExists("Enabled", "Hoppie") ? Boolean.Parse(settingsFile.Read("Enabled", "Hoppie")) : false;
                settingHoppieLogon = settingsFile.KeyExists("LogonCode", "Hoppie") ? settingsFile.Read("LogonCode", "Hoppie") : null;
                settingPrivateEnabled = settingsFile.KeyExists("Enabled", "RelayPrivate") ? Boolean.Parse(settingsFile.Read("Enabled", "RelayPrivate")) : false;
                settingRadioEnabled = settingsFile.KeyExists("Enabled", "RelayRadio") ? Boolean.Parse(settingsFile.Read("Enabled", "RelayRadio")) : false;
                settingSelcalEnabled = settingsFile.KeyExists("Enabled", "RelaySelcal") ? Boolean.Parse(settingsFile.Read("Enabled", "RelaySelcal")) : false;
                settingTelegramBotToken = settingsFile.KeyExists("BotToken", "Telegram") ? settingsFile.Read("BotToken", "Telegram") : null;
                settingTelegramChatId = settingsFile.KeyExists("ChatId", "Telegram") ? settingsFile.Read("ChatId", "Telegram") : null;
                settingDisconnectEnabled = settingsFile.KeyExists("Enabled", "Disconnect") ? Boolean.Parse(settingsFile.Read("Enabled", "Disconnect")) : false;
                settingGotifyUrl = settingsFile.KeyExists("Url", "Gotify") ? settingsFile.Read("Url", "Gotify") : null;
                settingGotifyToken = settingsFile.KeyExists("Token", "Gotify") ? settingsFile.Read("Token", "Gotify") : null;

                // Validate values
                if (settingHoppieEnabled && settingHoppieLogon == null) {
                    sendDebug("Hoppie logon code not set. Check your vPilot-Pushover.ini");
                    notifier.sendMessage("Hoppie logon code not set. Check your vPilot-Pushover.ini");
                }

                settingsLoaded = true;

            } else {
                sendDebug("Registry key not found. Is vPilot installed correctly?");
            }

        }

        /*
         * 
         * Check if is update available
         *
        */
        private async void updateChecker() {

            using (HttpClient httpClient = new HttpClient()) {
                try {
                    HttpResponseMessage response = await httpClient.GetAsync("https://raw.githubusercontent.com/blt950/vPilot-Pushover/main/version.txt");
                    if (response.IsSuccessStatusCode) {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        if (responseContent != (version)) {
                            await Task.Delay(5000);
                            sendDebug($"Update available. Latest version is v{responseContent}");
                            notifier.sendMessage($"Update available. Latest version is v{responseContent}. Download newest version at https://blt950.com", "vPilot Pushover Plugin");
                        }
                    } else {
                        sendDebug($"[Update Checker] HttpResponse request failed with status code: {response.StatusCode}");
                    }
                } catch (Exception ex) {
                    sendDebug($"[Update Checker] An HttpResponse error occurred: {ex.Message}");
                }
            }

        }


    }
}