using System;
using Microsoft.Web.WebView2.Wpf;

namespace imgsaver
{
    /// <summary>
    /// Placeholder for split panel UI management - can be extended in future versions
    /// </summary>
    public class SplitPanel
    {
        public string PanelId { get; set; }
        
        public SplitPanel()
        {
            PanelId = Guid.NewGuid().ToString();
        }
    }
}
