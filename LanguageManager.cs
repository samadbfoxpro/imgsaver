using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace imgsaver
{
    public static class LanguageManager
    {
        private static string _currentLanguage = "en";
        public static string CurrentLanguage
        {
            get => _currentLanguage;
            set => _currentLanguage = value;
        }

        public static void ApplyLanguage(string lang)
        {
            _currentLanguage = lang.ToLower() == "fa" ? "fa" : "en";

            try
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;

                // Load the new resource dictionary
                string dictName = _currentLanguage == "fa" ? "Resources.fa.xaml" : "Resources.en.xaml";
                var newDict = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/Resources.{_currentLanguage}.xaml", UriKind.Absolute)
                };

                // Remove existing language dictionary if any
                var existing = app.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && 
                                         (d.Source.OriginalString.Contains("Resources.en.xaml") || 
                                          d.Source.OriginalString.Contains("Resources.fa.xaml")));
                
                if (existing != null)
                {
                    app.Resources.MergedDictionaries.Remove(existing);
                }

                // Add new dictionary
                app.Resources.MergedDictionaries.Add(newDict);

                // Update flow direction for all currently open windows
                UpdateAllWindowsFlowDirection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error applying language: " + ex.Message);
            }
        }

        public static void ApplyWindowLanguage(Window window)
        {
            if (window == null) return;
            
            // Keep Browser, BrowserSettings, MiniClipboard, PromptTagger, Gallery, ImageViewer, AuthLock, PersonaInjector and prompt editors always LeftToRight
            if (window is BrowserWindow || 
                window is BrowserSettingsWindow || 
                window is MiniClipboardWindow || 
                window is PromptTaggerWindow || 
                window is GalleryWindow || 
                window is ImageViewerWindow || 
                window is AuthLockWindow ||
                window is PersonaInjectorWindow ||
                window is CharacterEditorWindow ||
                window is PromptEditorWindow ||
                window is ExtraItemEditorWindow ||
                window is ExtraPromptEditorWindow ||
                window is PromptSurgeonWindow)
            {
                window.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                return;
            }

            window.FlowDirection = (_currentLanguage == "fa") ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
        }

        public static void UpdateAllWindowsFlowDirection()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                ApplyWindowLanguage(window);
            }
        }

        /// <summary>
        /// Reads the language config from the settings file.
        /// </summary>
        public static string LoadLanguageFromConfig()
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("config.txt");
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 12)
                    {
                        return lines[12].Trim().ToLower() == "fa" ? "fa" : "en";
                    }
                }
            }
            catch { }
            return "en";
        }
    }
}
