using System;

namespace imgsaver
{
    public enum InputEventType { MouseMove, MouseDown, MouseUp, MouseWheel, KeyDown, KeyUp }

    public class InputEvent
    {
        // Time in milliseconds since recording started
        public long T { get; set; }
        public InputEventType Type { get; set; }

        // Mouse
        public int X { get; set; }
        public int Y { get; set; }
        public string Button { get; set; } = ""; // "Left"/"Right"/"Middle"
        public int WheelDelta { get; set; }

        // Key
        public int KeyCode { get; set; }

        // Window Relativity Logic
        public bool IsRelative { get; set; }

        public override string ToString() => $"{T}ms {Type} ({X},{Y}) Rel:{IsRelative} {Button} Key:{KeyCode} Delta:{WheelDelta}";
    }
}