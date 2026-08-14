using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace imgsaver
{
    public partial class PromptTaggerWindow : Window
    {
        private readonly System.Windows.Media.Brush _activeTabBorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#06B6D4"));
        private readonly System.Windows.Media.Brush _inactiveTabBorderBrush = System.Windows.Media.Brushes.Transparent;
        private readonly System.Windows.Media.Brush _activeTextBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#06B6D4"));
        private readonly System.Windows.Media.Brush _inactiveTextBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8899AA"));

        private string _initialRawPrompt = "";
        private bool _isUpdatingLiveDiffs = false;
        private readonly Dictionary<string, string> _interactiveTagMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PromptTaggerWindow()
        {
            InitializeComponent();
            LanguageManager.ApplyWindowLanguage(this);
            
            TouchRightClickHelper.Register(TxtTemplate);
            TouchRightClickHelper.Register(TxtValues);
            TouchRightClickHelper.Register(TxtReplacerPrefix);
            TouchRightClickHelper.Register(TxtReplacerOutput);
            
            TouchRightClickHelper.Register(TxtPromptA);
            TouchRightClickHelper.Register(TxtPromptB);
            TouchRightClickHelper.Register(TxtDiffPrefix);
            TouchRightClickHelper.Register(TxtDiffTemplateOutput);
            TouchRightClickHelper.Register(TxtDiffValuesOutput);

            TouchRightClickHelper.Register(TxtInteractivePrompt);
            TouchRightClickHelper.Register(TxtLiveDifferences);
            TouchRightClickHelper.Register(TxtInteractivePrefix);

            Loaded += PromptTaggerWindow_Loaded;
            Closed += PromptTaggerWindow_Closed;
        }

        private void PromptTaggerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Restore persistent values from the store
            TxtTemplate.Text = PromptTaggerStore.Template;
            TxtValues.Text = PromptTaggerStore.Values;
            TxtReplacerPrefix.Text = string.IsNullOrEmpty(PromptTaggerStore.Prefix) ? "PH_" : PromptTaggerStore.Prefix;
            
            TxtPromptA.Text = PromptTaggerStore.DiffPromptA;
            TxtPromptB.Text = PromptTaggerStore.DiffPromptB;
            TxtDiffPrefix.Text = string.IsNullOrEmpty(PromptTaggerStore.DiffPrefix) ? "PH_" : PromptTaggerStore.DiffPrefix;
            TxtReplacerOutput.Text = PromptTaggerStore.ReplacerOutput;
            TxtDiffTemplateOutput.Text = PromptTaggerStore.DiffTemplateOutput;
            TxtDiffValuesOutput.Text = PromptTaggerStore.DiffValuesOutput;

            TxtInteractivePrompt.Text = PromptTaggerStore.InteractivePrompt;
            _initialRawPrompt = string.IsNullOrEmpty(PromptTaggerStore.InteractiveInitial) ? TxtInteractivePrompt.Text : PromptTaggerStore.InteractiveInitial;
            TxtLiveDifferences.Text = PromptTaggerStore.InteractiveValues;
            TxtInteractivePrefix.Text = string.IsNullOrEmpty(PromptTaggerStore.InteractivePrefix) ? "PH_" : PromptTaggerStore.InteractivePrefix;

            SwitchToTab(0); // Replacer default
        }

        private void PromptTaggerWindow_Closed(object? sender, EventArgs e)
        {
            SaveCurrentState();
        }

        private void SaveCurrentState()
        {
            PromptTaggerStore.Template = TxtTemplate.Text;
            PromptTaggerStore.Values = TxtValues.Text;
            PromptTaggerStore.Prefix = TxtReplacerPrefix.Text;
            PromptTaggerStore.DiffPromptA = TxtPromptA.Text;
            PromptTaggerStore.DiffPromptB = TxtPromptB.Text;
            PromptTaggerStore.DiffPrefix = TxtDiffPrefix.Text;
            PromptTaggerStore.ReplacerOutput = TxtReplacerOutput.Text;
            PromptTaggerStore.DiffTemplateOutput = TxtDiffTemplateOutput.Text;
            PromptTaggerStore.DiffValuesOutput = TxtDiffValuesOutput.Text;

            PromptTaggerStore.InteractivePrompt = TxtInteractivePrompt.Text;
            PromptTaggerStore.InteractiveInitial = _initialRawPrompt;
            PromptTaggerStore.InteractiveValues = TxtLiveDifferences.Text;
            PromptTaggerStore.InteractivePrefix = TxtInteractivePrefix.Text;

            PromptTaggerStore.Save();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ─── Tab Navigation ──────────────────────────────────────────────

        private void BtnTabReplacer_Click(object sender, RoutedEventArgs e) => SwitchToTab(0);
        private void BtnTabDiff_Click(object sender, RoutedEventArgs e) => SwitchToTab(1);
        private void BtnTabInteractive_Click(object sender, RoutedEventArgs e) => SwitchToTab(2);

        private void SwitchToTab(int tabIndex)
        {
            PanelReplacer.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            PanelDiff.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelInteractive.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

            BtnTabReplacer.BorderBrush = tabIndex == 0 ? _activeTabBorderBrush : _inactiveTabBorderBrush;
            BtnTabReplacer.Foreground = tabIndex == 0 ? _activeTextBrush : _inactiveTextBrush;

            BtnTabDiff.BorderBrush = tabIndex == 1 ? _activeTabBorderBrush : _inactiveTabBorderBrush;
            BtnTabDiff.Foreground = tabIndex == 1 ? _activeTextBrush : _inactiveTextBrush;

            BtnTabInteractive.BorderBrush = tabIndex == 2 ? _activeTabBorderBrush : _inactiveTabBorderBrush;
            BtnTabInteractive.Foreground = tabIndex == 2 ? _activeTextBrush : _inactiveTextBrush;
        }

        // ─── Tab 1: Replacer Logic ────────────────────────────────────────

        private void BtnProcessReplacement_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string template = TxtTemplate.Text.Replace("\r", " ").Replace("\n", " ");
                while (template.Contains("  ")) template = template.Replace("  ", " ");
                
                string rawValues = TxtValues.Text.Replace("\r", " ").Replace("\n", " ");
                while (rawValues.Contains("  ")) rawValues = rawValues.Replace("  ", " ");

                string prefix = TxtReplacerPrefix.Text.Trim();
                if (string.IsNullOrEmpty(prefix)) prefix = "PH_";

                var values = rawValues.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(v => v.Trim())
                                      .Where(v => !string.IsNullOrEmpty(v))
                                      .ToList();

                var tagPattern = $@"\[{Regex.Escape(prefix)}\d+\]";
                var regex = new Regex(tagPattern, RegexOptions.IgnoreCase);

                PromptTaggerStore.Template = template;
                PromptTaggerStore.Values = rawValues;
                PromptTaggerStore.Prefix = prefix;

                int valIndex = 0;
                string result = regex.Replace(template, m =>
                {
                    if (valIndex < values.Count)
                    {
                        string replacement = values[valIndex];
                        valIndex++;
                        return replacement;
                    }
                    return m.Value; // leave unreplaced if no values left
                });

                TxtReplacerOutput.Text = result;
                SaveCurrentState();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error processing replacement:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopyReplacerOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtReplacerOutput.Text))
            {
                try
                {
                    System.Windows.Clipboard.SetText(TxtReplacerOutput.Text);
                }
                catch { }
            }
        }

        // ─── Tab 2: Comparator / Diff Logic ──────────────────────────────

        private void BtnComparePrompts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rawA = TxtPromptA.Text.Replace("\r", " ").Replace("\n", " ");
                while (rawA.Contains("  ")) rawA = rawA.Replace("  ", " ");

                string rawB = TxtPromptB.Text.Replace("\r", " ").Replace("\n", " ");
                while (rawB.Contains("  ")) rawB = rawB.Replace("  ", " ");
                string prefix = TxtDiffPrefix.Text.Trim();
                if (string.IsNullOrEmpty(prefix)) prefix = "PH_";

                var clausesA = rawA.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(c => c.Trim())
                                   .Where(c => !string.IsNullOrEmpty(c))
                                   .ToList();

                var clausesB = rawB.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(c => c.Trim())
                                   .Where(c => !string.IsNullOrEmpty(c))
                                   .ToList();

                // Bidirectional auto-alignment: detect which is target (longer) and reference (shorter)
                bool isBTarget = clausesB.Count >= clausesA.Count;
                string targetRaw = isBTarget ? rawB : rawA;
                var refClauses = isBTarget ? clausesA : clausesB;
                var refSet = new HashSet<string>(refClauses, StringComparer.OrdinalIgnoreCase);

                var parts = targetRaw.Split(',');
                var resultParts = new List<string>();
                var extractedDiffs = new List<string>();
                int tagIndex = 1;

                var tagRegex = new Regex($@"^\s*\[{Regex.Escape(prefix)}\d+\]\s*$", RegexOptions.IgnoreCase);

                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        resultParts.Add(part);
                        continue;
                    }

                    bool isRef = refSet.Contains(trimmed);
                    bool isAlreadyTag = tagRegex.IsMatch(part);

                    if (!isRef && !isAlreadyTag)
                    {
                        extractedDiffs.Add(trimmed);
                        
                        // Preserve original layout spacing
                        int valPos = part.IndexOf(trimmed);
                        string leadingSpace = part.Substring(0, valPos);
                        string trailingSpace = part.Substring(valPos + trimmed.Length);

                        resultParts.Add($"{leadingSpace}[{prefix}{tagIndex}]{trailingSpace}");
                        tagIndex++;
                    }
                    else
                    {
                        resultParts.Add(part);
                    }
                }

                TxtDiffTemplateOutput.Text = string.Join(",", resultParts);
                TxtDiffValuesOutput.Text = string.Join(", ", extractedDiffs);
                SaveCurrentState();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error comparing prompts:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTransferToReplacer_Click(object sender, RoutedEventArgs e)
        {
            TxtTemplate.Text = TxtDiffTemplateOutput.Text;
            TxtValues.Text = TxtDiffValuesOutput.Text;
            TxtReplacerPrefix.Text = TxtDiffPrefix.Text;

            SaveCurrentState();
            SwitchToTab(0); // Switch to Tab 1
        }

        private void BtnTransferToExtra_Click(object sender, RoutedEventArgs e)
        {
            string extractedValues = TxtDiffValuesOutput.Text;
            
            // 1. Save template and prefix to the main tagger store
            PromptTaggerStore.Template = TxtDiffTemplateOutput.Text;
            PromptTaggerStore.Values = extractedValues;
            PromptTaggerStore.Prefix = TxtDiffPrefix.Text;
            PromptTaggerStore.DiffPromptA = TxtPromptA.Text;
            PromptTaggerStore.DiffPromptB = TxtPromptB.Text;
            PromptTaggerStore.DiffPrefix = TxtDiffPrefix.Text;
            
            // 2. Save manual values specifically for the extra panels
            PromptTaggerStore.ManualValues = extractedValues;
            PromptTaggerStore.Save();

            // 3. Dynamically update open panels
            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MiniExtraPanel panel)
                {
                    panel.TxtTaggerValues.Text = extractedValues;
                    panel.ChkUseTaggerValues.IsChecked = true;
                }
                else if (win is FloatingExtraWindow floatWin)
                {
                    floatWin.TxtTaggerValues.Text = extractedValues;
                    floatWin.ChkUseTaggerValues.IsChecked = true;
                }
            }

            CursorBadgeNotification.ShowTagReplaced("🧩 Sent to Extra Panels!");
        }

        // ─── Tab 3: Interactive Selection Tagger Logic ────────────────────

        private void BtnTagSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedText = TxtInteractivePrompt.SelectedText;
                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    System.Windows.MessageBox.Show("Please select a word or phrase in the text editor first!", "Interactive Tagger", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrEmpty(_initialRawPrompt))
                {
                    _initialRawPrompt = TxtInteractivePrompt.Text;
                }

                string prefix = TxtInteractivePrefix.Text.Trim();
                if (string.IsNullOrEmpty(prefix)) prefix = "PH_";

                // Count existing tags in the current text to get the next index
                var tagPattern = $@"\[{Regex.Escape(prefix)}(\d+)\]";
                var matches = Regex.Matches(TxtInteractivePrompt.Text, tagPattern, RegexOptions.IgnoreCase);
                int maxIdx = 0;
                foreach (Match m in matches)
                {
                    if (m.Groups.Count > 1 && int.TryParse(m.Groups[1].Value, out int idx))
                    {
                        if (idx > maxIdx) maxIdx = idx;
                    }
                }
                int nextIndex = maxIdx + 1;
                string newTag = $"[{prefix}{nextIndex}]";

                string trimmedSnippet = selectedText.Trim(' ', ',', ';');
                if (string.IsNullOrEmpty(trimmedSnippet)) trimmedSnippet = selectedText;

                // Save exact mapping from tag to selected snippet
                _interactiveTagMap[newTag] = trimmedSnippet;

                int selStart = TxtInteractivePrompt.SelectionStart;
                string currentText = TxtInteractivePrompt.Text;

                // Replace selected snippet with tag
                string updatedText = currentText.Remove(selStart, selectedText.Length).Insert(selStart, newTag);

                TxtInteractivePrompt.Text = updatedText;
                TxtInteractivePrompt.SelectionStart = selStart + newTag.Length;
                TxtInteractivePrompt.SelectionLength = 0;

                UpdateLiveDifferences();
                SaveCurrentState();
                CursorBadgeNotification.ShowTagReplaced($"🧩 Created Tag {newTag}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error tagging selection:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSetBasePrompt_Click(object sender, RoutedEventArgs e)
        {
            _initialRawPrompt = TxtInteractivePrompt.Text;
            TxtLiveDifferences.Text = "";
            SaveCurrentState();
            CursorBadgeNotification.ShowTagReplaced("🧩 Base Prompt Locked!");
        }

        private void TxtInteractivePrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingLiveDiffs) return;
            UpdateLiveDifferences();
        }

        private void TxtLiveDifferences_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Allowed manual adjustments to extracted diffs
        }

        private void UpdateLiveDifferences()
        {
            try
            {
                _isUpdatingLiveDiffs = true;
                string currentText = TxtInteractivePrompt.Text;
                string prefix = TxtInteractivePrefix.Text.Trim();
                if (string.IsNullOrEmpty(prefix)) prefix = "PH_";

                var tagPattern = $@"\[{Regex.Escape(prefix)}\d+\]";
                var matches = Regex.Matches(currentText, tagPattern, RegexOptions.IgnoreCase);

                var activeSnippets = new List<string>();
                foreach (Match m in matches)
                {
                    string tag = m.Value;
                    if (_interactiveTagMap.TryGetValue(tag, out string? snippet))
                    {
                        if (!string.IsNullOrWhiteSpace(snippet) && !activeSnippets.Contains(snippet, StringComparer.OrdinalIgnoreCase))
                        {
                            activeSnippets.Add(snippet);
                        }
                    }
                }

                TxtLiveDifferences.Text = string.Join(", ", activeSnippets);
            }
            catch { }
            finally
            {
                _isUpdatingLiveDiffs = false;
            }
        }

        private void BtnCopyInteractiveOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtInteractivePrompt.Text))
            {
                try
                {
                    System.Windows.Clipboard.SetText(TxtInteractivePrompt.Text);
                    CursorBadgeNotification.ShowTagReplaced("📋 Tagged Prompt Copied!");
                }
                catch { }
            }
        }

        private void BtnSendToMiniClip_Click(object sender, RoutedEventArgs e)
        {
            string values = TxtLiveDifferences.Text;
            if (string.IsNullOrWhiteSpace(values))
            {
                System.Windows.MessageBox.Show("No extracted values to send!", "Interactive Tagger", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PromptTaggerStore.Template = TxtInteractivePrompt.Text;
            PromptTaggerStore.Prefix = TxtInteractivePrefix.Text.Trim();
            PromptTaggerStore.Values = values;
            PromptTaggerStore.ManualValues = values;
            SaveCurrentState();

            // Broadcast to open extra windows
            foreach (Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MiniExtraPanel panel)
                {
                    panel.TxtTaggerValues.Text = values;
                    panel.ChkUseTaggerValues.IsChecked = true;
                }
                else if (win is FloatingExtraWindow floatWin)
                {
                    floatWin.TxtTaggerValues.Text = values;
                    floatWin.ChkUseTaggerValues.IsChecked = true;
                }
            }

            CursorBadgeNotification.ShowTagReplaced("🚀 Set & Sent to MiniClip!");
        }

        private void MenuClear_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && 
                menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu && 
                contextMenu.PlacementTarget is System.Windows.Controls.TextBox textBox)
            {
                textBox.Text = "";
            }
        }
    }
}
