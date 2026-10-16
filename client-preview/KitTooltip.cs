using System;
using System.Collections;
using System.Reflection;
using EFT;
using UnityEngine;

namespace WeekendDropsUI.Services
{
    public static partial class NativeAudio
    {
        private static Action<Rect, Sprite, Color> drawIcon;
        public static void BeginKitFrame() { hoveredKit = null; }
        private static object hoveredKit;
        private static object cachedKit;
        private static string[] extraTemplates;
        private static int[] extraCounts;
        private static Action<string> requestIcon;
        private static Func<string, Sprite> getIcon;
        private static GUIStyle kitText;

        private static object CardValue(object card, string name)
        {
            return card.GetType().GetProperty(name).GetValue(card, null);
        }

        public static void HoverKit(Rect cardRect, object card)
        {
            if (card == null || !cardRect.Contains(Event.current.mousePosition)) return;
            if (!(bool)CardValue(card, "Revealed")) return;
            if ((bool)CardValue(card, "Mystery") && !(bool)CardValue(card, "Bought")) return;
            var extras = CardValue(card, "Extras") as IList;
            if (extras == null || extras.Count == 0) return;
            hoveredKit = card;
            if (ReferenceEquals(cachedKit, card)) return;
            cachedKit = card;
            extraTemplates = new string[extras.Count];
            extraCounts = new int[extras.Count];
            for (int i = 0; i < extras.Count; i++)
            {
                extraTemplates[i] = (string)CardValue(extras[i], "TemplateId");
                extraCounts[i] = (int)CardValue(extras[i], "Count");
            }
        }

        public static void DrawKitTooltip()
        {
            if (hoveredKit == null || extraTemplates == null) return;
            if (getIcon == null)
            {
                var cache = typeof(NativeAudio).Assembly.GetType("WeekendDropsUI.Services.WdItemIconCache");
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                drawIcon = (Action<Rect, Sprite, Color>)Delegate.CreateDelegate(typeof(Action<Rect, Sprite, Color>), cache.GetMethod("Draw", flags));
                getIcon = (Func<string, Sprite>)Delegate.CreateDelegate(typeof(Func<string, Sprite>), cache.GetMethod("Get", flags));
                requestIcon = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), cache.GetMethod("Request", flags));
            }
            if (kitText == null)
            {
                kitText = new GUIStyle(GUI.skin.label);
                kitText.wordWrap = true;
                kitText.fontSize = 14;
                kitText.normal.textColor = Color.white;
            }
            Vector3 screen = GUI.matrix.inverse.MultiplyPoint(new Vector3(Screen.width, Screen.height, 0));
            int rows = Mathf.Max(1, (int)((screen.y - 70) / 58));
            int columns = (extraTemplates.Length + rows - 1) / rows;
            rows = Mathf.Min(rows, extraTemplates.Length);
            float columnWidth = Mathf.Min(320, (screen.x - 24) / columns);
            float width = columnWidth * columns, height = rows * 58 + 16;
            Vector2 mouse = Event.current.mousePosition;
            float x = mouse.x + 18;
            if (x + width > screen.x - 12) x = mouse.x - width - 18;
            x = Mathf.Clamp(x, 12, Mathf.Max(12, screen.x - width - 12));
            float y = Mathf.Clamp(mouse.y + 18, 12, Mathf.Max(12, screen.y - height - 12));
            var previous = GUI.color;
            GUI.color = new Color(0.055f, 0.06f, 0.07f, 0.98f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            for (int i = 0; i < extraTemplates.Length; i++)
            {
                string id = extraTemplates[i];
                if (string.IsNullOrEmpty(id)) continue;
                float rowX = x + (i / rows) * columnWidth + 8, rowY = y + (i % rows) * 58 + 8;
                requestIcon(id);
                Sprite icon = getIcon(id);
                if (icon != null && drawIcon != null) drawIcon(new Rect(rowX, rowY, 62, 50), icon, Color.white);
                string name = LocalizationExtensions.Localized(id + " Name", (string)null);
                GUI.Label(new Rect(rowX + 70, rowY, columnWidth - 86, 50), extraCounts[i] + " × " + name, kitText);
            }
            GUI.color = previous;
        }
    }
}
