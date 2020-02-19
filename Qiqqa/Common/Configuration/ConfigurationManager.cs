﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Windows;
using Qiqqa.WebBrowsing.GeckoStuff;
using Utilities;
using Utilities.Files;
using Utilities.Internet;
using Utilities.Reflection;
using Utilities.Shutdownable;
using Utilities.Strings;
using Newtonsoft.Json;
using UConf = Utilities.Configuration;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using Utilities.Misc;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Qiqqa.Common.Configuration
{
    public class ConfigurationManager
    {
        // https://stackoverflow.com/questions/10465961/potential-pitfalls-with-static-constructors-in-c-sharp
        private static readonly Lazy<ConfigurationManager> __instance = new Lazy<ConfigurationManager>(() =>
        {
            return new ConfigurationManager();
        });
        public static ConfigurationManager Instance
        {
            get
            {
                return __instance.Value;
            }
        }

        private string user_guid;

        public string StartupDirectoryForQiqqa => UnitTestDetector.StartupDirectoryForQiqqa;

        private string base_directory_for_qiqqa = null;
        public string BaseDirectoryForQiqqa
        {
            get
            {
                if (null == base_directory_for_qiqqa)
                {
                    string override_path = RegistrySettings.Instance.Read(RegistrySettings.BaseDataDirectory);
                    if (!String.IsNullOrEmpty(override_path))
                    {
                        override_path = override_path.Trim();
                        if (!String.IsNullOrEmpty(override_path))
                        {
                            base_directory_for_qiqqa = Path.GetFullPath(override_path);

                            // Check that the path is reasonable
                            try
                            {
                                Directory.CreateDirectory(base_directory_for_qiqqa);
                            }
                            catch (Exception ex)
                            {
                                Logging.Error(ex, "There was a problem creating the user-overridden base directory '{0}', so reverting to default", base_directory_for_qiqqa);
                                base_directory_for_qiqqa = null;
                            }
                        }
                    }

                    // If we get here, use the default path
                    if (null == base_directory_for_qiqqa)
                    {
                        base_directory_for_qiqqa = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Quantisle/Qiqqa"));
                    }
                }

                return base_directory_for_qiqqa;
            }
        }

        public string BaseDirectoryForUser => Path.GetFullPath(Path.Combine(BaseDirectoryForQiqqa, user_guid));

        private string ConfigFilenameForUser => Path.Combine(BaseDirectoryForUser, @"Qiqqa.configuration");

        private string SearchHistoryFilename => Path.Combine(BaseDirectoryForUser, @"Qiqqa.search_history");

        public string Program7ZIP => Path.Combine(StartupDirectoryForQiqqa, @"7za.exe");

        public string ProgramHTMLToPDF => Path.Combine(StartupDirectoryForQiqqa, @"wkhtmltopdf.exe");

        public string DeveloperTestSettingsFilename => Path.Combine(BaseDirectoryForQiqqa, @"Qiqqa.Developer.Settings.json5");

        private Dictionary<string, object> developer_test_settings = null;
        private ConfigurationRecord configuration_record;
        private AugmentedBindable<ConfigurationRecord> configuration_record_bindable;

        private ConfigurationManager()
        {
            ShutdownableManager.Instance.Register(Shutdown);

            UConf.GetWebUserAgent = () => ConfigurationManager.Instance.ConfigurationRecord.GetWebUserAgent();
            UConf.GetProxy = () => ConfigurationManager.Instance.Proxy;

            ResetConfigurationRecordToGuest();
        }

        private void Shutdown()
        {
            Logging.Info("ConfigurationManager is saving the configuration at shutdown");
            SaveConfigurationRecord(force_save: true);
            SaveSearchHistory();
        }

        private void ResetConfigurationRecord(string user_guid_)
        {
            Logging.Info("Resetting configuration settings to {0}", user_guid_);

            if (null != configuration_record_bindable)
            {
                configuration_record_bindable.PropertyChanged -= configuration_record_bindable_PropertyChanged;
            }

            // Create the new user_guid
            user_guid = user_guid_;

            // Create the base directory in case it doesnt exist
            Directory.CreateDirectory(BaseDirectoryForUser);

            // Try loading any pre-existing config file
            try
            {
                // First try to load the modern configuration file type: JSON format
                if (File.Exists(ConfigFilenameForUser + ".json"))
                {
                    Logging.Info("Loading configuration from JSON file {0}", ConfigFilenameForUser + ".json");
                    string input;
                    // The using statement automatically CLOSES the stream and calls 
                    // IDisposable.Dispose on the stream object.
                    // NOTE: do not use FileStream for text files because it read bytes, but StreamReader
                    // encodes the output as text.
                    using (System.IO.StreamReader file = new System.IO.StreamReader(ConfigFilenameForUser + ".json"))
                    {
                        input = file.ReadToEnd();
                    }
                    configuration_record = JsonConvert.DeserializeObject<ConfigurationRecord>(input);
                    Logging.Info("Loaded configuration from JSON file {0}", ConfigFilenameForUser + ".json");
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem loading configuration from JSON file {0}", ConfigFilenameForUser + ".json");
            }

            // If the new JSON format doesn't fly, we're probably migrating from an older Qiqqa like v80 or v79:
            // try loading the config the old way:
            if (null == configuration_record)
            {
                // Try loading any pre-existing config file: call The Migrator
                try
                {
                    if (File.Exists(ConfigFilenameForUser))
                    {
	                    Logging.Info("Loading configuration file {0}", ConfigFilenameForUser);
                        configuration_record = (ConfigurationRecord)ObjectSerializer.LoadObject(ConfigFilenameForUser);
    	                Logging.Info("Loaded configuration file {0}", ConfigFilenameForUser);
                    }
                }
                catch (Exception ex)
                {
        	        Logging.Error(ex, "There was a problem loading configuration file {0}", ConfigFilenameForUser);
                }
            }

            if (null == configuration_record)
            {
                Logging.Info("There is no existing configuration, so creating one from scratch. Is configuration file {0} missing?", ConfigFilenameForUser);
                configuration_record = new ConfigurationRecord();
            }

#if TEST_FIRST_TIME_QIQQA_EXECUTION
            // Testing....
            configuration_record.TermsAndConditionsAccepted = false;
            configuration_record.SyncTermsAccepted = false;
#endif

            // Attach a bindable to the configuration record so that GUIs can update it
            configuration_record_bindable = new AugmentedBindable<ConfigurationRecord>(configuration_record);
            configuration_record_bindable.PropertyChanged += configuration_record_bindable_PropertyChanged;

#if false
            // Make sure we have a GA tracking ID
            if (String.IsNullOrEmpty(configuration_record.Feedback_GATrackingCode))
            {
                configuration_record.Feedback_GATrackingCode = Guid.NewGuid().ToString();
            }
#endif

            // Also see if we have a Developer Test Settings file, which contains development/test environment overrides:
            try
            {
                if (File.Exists(DeveloperTestSettingsFilename))
                {
                    Logging.Info("Loading developer test settings file {0}", DeveloperTestSettingsFilename);

                    // see also https://www.newtonsoft.com/json/help/html/SerializationErrorHandling.htm

                    List<string> errors = new List<string>();

                    developer_test_settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                        File.ReadAllText(DeveloperTestSettingsFilename),
                        new JsonSerializerSettings
                        {
                            Error = delegate (object sender, ErrorEventArgs args)
                            {
                                errors.Add(args.ErrorContext.Error.Message);
                                args.ErrorContext.Handled = true;
                            },
                            //Converters = { new IsoDateTimeConverter() }
                        });

                    Logging.Info("Loaded developer test settings file {0}: {1}", DeveloperTestSettingsFilename, errors.Count == 0 ? "no errors" : errors.ToString());
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem loading developer test settings file {0}", DeveloperTestSettingsFilename);
            }
        }

        private void configuration_record_bindable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == configuration_record_bindable)
            {
                // Saving the config on propertyy change is non-essential as another save action will be triggered
                // from the application shutdown handler anyway. Therefor we delegate the inherent file I/O to 
                // a background task:
                SafeThreadPool.QueueUserWorkItem(o =>
                {
                    SaveConfigurationRecord();
                }, skip_task_at_app_shutdown: true);

                GeckoManager.SetupProxyAndUserAgent(false);
            }
            else
            {
                Logging.Warn("Not saving configuration record from old bindable wrapper");
            }
        }

        private Stopwatch lastSaveTimestamp = null;

        public void SaveConfigurationRecord(bool force_save = false)
        {
            if (lastSaveTimestamp == null)
            {
                lastSaveTimestamp = Stopwatch.StartNew();
                force_save = true;
            }
            else
            {
                long elapsed = lastSaveTimestamp.ElapsedMilliseconds;
                lastSaveTimestamp.Restart();
                if (!force_save)
                {
                    // only update the stored config file every minute or so, no matter how many changes are made.
                    force_save = (elapsed >= 60 * 1000);
                }
            }
            // if the flag hasn't been set by now, we're looking at a too frequent request to save the qiqqa config file.
            if (!force_save)
            {
                return;
            }

#if false           // we don't 'do' old configuration file format anymore on (re)write; instead we use portable JSON
            try
            {
                Logging.Info("Saving configuration");
                ObjectSerializer.SaveObject(ConfigFilenameForUser, configuration_record);
                Logging.Info("Saved configuration");
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem saving the configuration.");
            }
#endif

            try
            {
                Logging.Info("Saving configuration to JSON");
                string output = JsonConvert.SerializeObject(configuration_record, Formatting.Indented);

                // The using statement automatically flushes AND CLOSES the stream and calls 
                // IDisposable.Dispose on the stream object.
                // NOTE: do not use FileStream for text files because it writes bytes, but StreamWriter
                // encodes the output as text.
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(ConfigFilenameForUser + ".json"))
                {
                    file.WriteLine(output);
                }
                Logging.Info("Saved configuration to JSON");
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem saving the configuration to JSON.");
            }
        }

#region --- Search history ----------------------------------------------------------------------------------------

        private HashSet<string> search_history = new HashSet<string>();
        private HashSet<string> search_history_from_disk = null;
        public HashSet<string> SearchHistory
        {
            get
            {
                // Add any user specified searches
                var user_specified_searches = ConfigurationManager.Instance.ConfigurationRecord.GUI_UserDefinedSearchStrings;
                if (!String.IsNullOrEmpty(user_specified_searches))
                {
                    foreach (string search in StringTools.splitAtNewline(user_specified_searches))
                    {
                        search_history.Add(search);
                    }
                }

                if (null == search_history_from_disk)
                {
                    search_history_from_disk = new HashSet<string>();
                    // Try to load it from disk (if we are premium or premium+)
                    try
                    {
                        if (File.Exists(SearchHistoryFilename))
                        {
                            foreach (string search in File.ReadAllLines(SearchHistoryFilename))
                            {
                                search_history.Add(search);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Error(ex, "There was a problem loading the search history.");
                    }
                }
                foreach (string search in search_history_from_disk)
                {
                    search_history.Add(search);
                    if (search_history.Count > 100) break;
                }

                return search_history;
            }
        }

        private void SaveSearchHistory()
        {
            try
            {
                File.WriteAllLines(SearchHistoryFilename, SearchHistory);
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem saving the search history.");
            }
        }

#endregion

#region --- Public initialisation ----------------------------------------------------------------------------------------

        public void ResetConfigurationRecordToGuest()
        {
            ResetConfigurationRecord("Guest");
        }

        public void ResetConfigurationRecordToUser(string user_guid_)
        {
            ResetConfigurationRecord(user_guid_);
        }

#endregion

#region --- Public accessors ----------------------------------------------------------------------------------------

        public Visibility NoviceVisibility => ConfigurationRecord.GUI_IsNovice ? Visibility.Collapsed : Visibility.Visible;

        public ConfigurationRecord ConfigurationRecord
        {
            get
            {
                if (null == configuration_record)
                {
                    Logging.Warn("Accessing ConfigurationRecord before it has been initialized by Qiqqa: running as Guest for now");
                    ResetConfigurationRecordToGuest();
                }
                return configuration_record;
            }
        }

        public static bool IsEnabled(string key)
        {
            if (null != Instance.developer_test_settings)
            {
                if (Instance.developer_test_settings.TryGetValue(key, out var val))
                {
                    bool? rv = val as bool?;
                    return rv ?? true;
                }
            }
            return true;
        }

        public static void ThrowWhenActionIsNotEnabled(string key)
        {
            if (!IsEnabled(key))
            {
                throw new ApplicationException($"{key} is not enabled in the developer test settings.");
            }
        }

        public AugmentedBindable<ConfigurationRecord> ConfigurationRecord_Bindable => configuration_record_bindable;

        /// <summary>
        /// Uses the proxy settings specified by the user.  Otherwise defaults to the system proxy.
        /// </summary>
        public IWebProxy Proxy
        {
            get
            {
                string hostname = ConfigurationRecord.Proxy_Hostname;
                if (!String.IsNullOrEmpty(hostname))
                {
                    hostname = hostname.Trim();
                    hostname = hostname.ToLower();
                    hostname = hostname.Replace("http://", "");
                    hostname = hostname.Replace("https://", "");
                }

                string username = ConfigurationRecord.Proxy_Username;
                if (!String.IsNullOrEmpty(username)) username = username.Trim();

                string password = ConfigurationRecord.Proxy_Password;
                if (!String.IsNullOrEmpty(password)) password = password.Trim();

                return ProxyTools.CreateProxy(ConfigurationRecord.Proxy_UseProxy, hostname, ConfigurationRecord.Proxy_Port, username, password);
            }
        }

#endregion
    }
}
