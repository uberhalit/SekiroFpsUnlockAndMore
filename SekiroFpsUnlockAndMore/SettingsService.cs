using System;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SekiroFpsUnlockAndMore
{
    [XmlRoot("SekiroFpsUnlockAndMore")]
    [Serializable]
    public class ApplicationSettings
    {
        /**
         * Settings definition
         */
        [XmlElement]
        public bool gameVersionNotify { get; set; }
        [XmlElement]
        public bool cameraAdjustNotify { get; set; }
        [XmlElement]
        public bool peasantInput { get; set; }
        [XmlElement]
        public bool cbFramelock { get; set; }
        [XmlElement]
        public int tbFramelock { get; set; }
        [XmlElement]
        public bool cbAddResolution { get; set; }
        [XmlElement]
        public int tbWidth { get; set; }
        [XmlElement]
        public int tbHeight { get; set; }
        [XmlElement]
        public bool cbFov { get; set; }
        [XmlElement]
        public int tbFov { get; set; }
        [XmlElement]
        public bool cbBorderless { get; set; }
        [XmlElement]
        public bool cbBorderlessStretch { get; set; }
        [XmlElement]
        public bool cbLogStats { get; set; }
        [XmlElement]
        public bool exGameMods { get; set; }
        [XmlElement]
        public bool cbCamAdjust { get; set; }
        [XmlElement]
        public bool cbCamReset { get; set; }
        [XmlElement]
        public bool cbAutoLoot { get; set; }
        [XmlElement]
        public bool cbDragonrot { get; set; }
        [XmlElement]
        public bool cbDeathPenalty { get; set; }
        [XmlElement]
        public int hiddenDPs { get; set; }
        [XmlElement]
        public bool cbEmblemUpgrade { get; set; }
        [XmlElement]
        public bool cbGameSpeed { get; set; }
        [XmlElement]
        public int tbGameSpeed { get; set; }
        [XmlElement]
        public bool cbPlayerSpeed { get; set; }
        [XmlElement]
        public int tbPlayerSpeed { get; set; }
        [XmlElement]
        public bool exGuide { get; set; }
    }

    public class SettingsService
    {
        private readonly string _sConfigurationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\config.xml";

        /// <summary>
        /// Read and store settings here.
        /// </summary>
        public ApplicationSettings ApplicationSettings;

        /// <summary>
        /// Create a settings provider to load and save settings.
        /// </summary>
        /// <param name="settingsFilePath">The file path to the settings file.</param>
        public SettingsService(string settingsFilePath = null)
        {
            if (settingsFilePath != null) _sConfigurationPath = settingsFilePath;
            ApplicationSettings = new ApplicationSettings();
        }

        /// <summary>
        /// Load settings from file into settings property.
        /// </summary>
        /// <returns></returns>
        internal bool Load()
        {
            if (!File.Exists(_sConfigurationPath)) return false;
            
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ApplicationSettings));
            using (StreamReader streamReader = new StreamReader(_sConfigurationPath))
            {
                try
                {
                    ApplicationSettings = (ApplicationSettings)xmlSerializer.Deserialize(streamReader);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while loading configuration file:\n" + ex.Message, "Sekiro FPS Unlocker and more");
                }
            }
            return false;
        }

        /// <summary>
        /// Save settings from settings property to file.
        /// </summary>
        internal void Save()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ApplicationSettings));
            using (StreamWriter streamWriter = new StreamWriter(_sConfigurationPath))
            { 
                try
                {
                    xmlSerializer.Serialize(streamWriter, ApplicationSettings);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while writing configuration file:\n" + ex.Message, "Sekiro FPS Unlocker and more");
                }
            }
        }

        /// <summary>
        /// Clears all settings and deletes the settings file.
        /// </summary>
        internal void Clear()
        {
            ApplicationSettings = new ApplicationSettings();
            try
            {
                if (File.Exists(_sConfigurationPath))
                    File.Delete(_sConfigurationPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while trying to delete configuration file:\n" + ex.Message, "Sekiro FPS Unlocker and more");
            }
        }
    }
}
