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
        private readonly System.Windows.Media.Brush _activeTabBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0284C7"));
        private readonly System.Windows.Media.Brush _inactiveTabBrush = System.Windows.Media.Brushes.Transparent;
        private readonly System.Windows.Media.Brush _activeTextBrush = System.Windows.Media.Brushes.White;
        private readonly System.Windows.Media.Brush _inactiveTextBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8899AA"));

        public PromptTaggerWindow()
        {
            InitializeComponent();
            SwitchToTab(true); // Replacer default
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ─── Tab Navigation ──────────────────────────────────────────────

        private void BtnTabReplacer_Click(object sender, RoutedEventArgs e) => SwitchToTab(true);
        private void BtnTabDiff_Click(object sender, RoutedEventArgs e) => SwitchToTab(false);

        private void SwitchToTab(bool showReplacer)
        {
            if (showReplacer)
            {
                PanelReplacer.Visibility = Visibility.Visible;
                PanelDiff.Visibility = Visibility.Collapsed;

                BtnTabReplacer.BorderBrush = _activeTabBrush;
                BtnTabReplacer.Foreground = _activeTextBrush;

                BtnTabDiff.BorderBrush = _inactiveTabBrush;
                BtnTabDiff.Foreground = _inactiveTextBrush;
            }
            else
            {
                PanelReplacer.Visibility = Visibility.Collapsed;
                PanelDiff.Visibility = Visibility.Visible;

                BtnTabReplacer.BorderBrush = _inactiveTabBrush;
                BtnTabReplacer.Foreground = _inactiveTextBrush;

                BtnTabDiff.BorderBrush = _activeTabBrush;
                BtnTabDiff.Foreground = _activeTextBrush;
            }
        }

        // ─── Tab 1: Replacer Logic ────────────────────────────────────────

        private void BtnProcessReplacement_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string template = TxtTemplate.Text;
                string rawValues = TxtValues.Text;
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
                string rawA = TxtPromptA.Text;
                string rawB = TxtPromptB.Text;
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

            PromptTaggerStore.Template = TxtDiffTemplateOutput.Text;
            PromptTaggerStore.Values = TxtDiffValuesOutput.Text;
            PromptTaggerStore.Prefix = TxtDiffPrefix.Text;

            SwitchToTab(true); // Switch to Tab 1
        }
    }
}
