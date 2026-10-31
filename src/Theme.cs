// LinkRadio: look of the window (car radio style). All textures are generated at runtime, no asset files needed.
using UnityEngine;

namespace LinkRadio
{
    internal static class Theme
    {
        public const float Width = 440f;
        public const float BodyWidth = Width - 2f - 28f;   // window border + body padding
        public const float HudWidth = 340f, HudNowHeight = 76f, HudNextHeight = 26f, HudGap = 4f, HudDefaultPos = 12f;

        public static readonly Color32 Amber = new Color32(0xff, 0xb4, 0x3a, 0xff);
        static readonly Color32 AmberHover = new Color32(0xff, 0xc6, 0x66, 0xff), AmberDown = new Color32(0xe0, 0x98, 0x28, 0xff);
        static readonly Color32 Panel = new Color32(0x17, 0x19, 0x1d, 0xff), HeaderBg = new Color32(0x20, 0x23, 0x29, 0xff), TabBg = new Color32(0x1b, 0x1e, 0x23, 0xff);
        static readonly Color32 Line = new Color32(0x33, 0x37, 0x3f, 0xff), LineStrong = new Color32(0x4a, 0x50, 0x5b, 0xff);
        static readonly Color32 Field = new Color32(0x0f, 0x11, 0x14, 0xff), Btn = new Color32(0x2a, 0x2e, 0x35, 0xff), BtnHover = new Color32(0x35, 0x3a, 0x43, 0xff), BtnDown = new Color32(0x20, 0x23, 0x29, 0xff);
        static readonly Color32 LcdBg = new Color32(0x0d, 0x0f, 0x0c, 0xff), LcdLine = new Color32(0x3a, 0x33, 0x20, 0xff), BarBg = new Color32(0x2c, 0x2a, 0x22, 0xff), SliderBg = new Color32(0x2f, 0x33, 0x3a, 0xff);
        static readonly Color32 ReqBg = new Color32(0x2b, 0x26, 0x16, 0xff), ReqLine = new Color32(0x5a, 0x4a, 0x20, 0xff), PillBg = new Color32(0x27, 0x33, 0x1f, 0xff);
        static readonly Color32 TextMain = new Color32(0xd9, 0xdc, 0xe2, 0xff), TextDim = new Color32(0x8f, 0x95, 0xa1, 0xff), TextHint = new Color32(0x7d, 0x84, 0x90, 0xff), TextOnAmber = new Color32(0x2a, 0x1c, 0x00, 0xff);
        static readonly Color32 TextReq = new Color32(0xff, 0xd2, 0x7a, 0xff), Good = new Color32(0xa9, 0xdc, 0x7f, 0xff), Bad = new Color32(0xff, 0x8c, 0x80, 0xff);
        static readonly Color32 Thumb = new Color32(0xe9, 0xeb, 0xef, 0xff), Clear = new Color32(0, 0, 0, 0);

        public static GUIStyle Window, Header, TabBar, Tab, TabOn, Body, Lcd, Request, Overlay;
        public static GUIStyle TitleText, Pill, Label, Hint, HintRight, Number, Who, StatusOk, StatusBad, Good1, ReqTitle;
        public static GUIStyle LcdSmall, LcdSmallRight, LcdTitle, Button, ButtonMain, TextField, Slider, SliderThumb, Check;
        public static GUIStyle HudWindow, HudNext, HudNextText, HudNextTime;
        public static Texture2D White, CheckOff, CheckOn, BarBack, BarFill, SliderBack;

        public static void Ensure()
        {
            if (Window != null && White != null && Window.normal.background != null) return;
            White = Solid(Color.white);
            BarBack = Round(10, 3, BarBg, BarBg); BarFill = Round(10, 3, Amber, Amber); SliderBack = Round(10, 3, SliderBg, SliderBg);
            CheckOff = Round(16, 4, Clear, LineStrong); CheckOn = MakeCheck();

            Window = Box(Round(28, 10, Panel, Line), 10, 1);
            Header = Box(Round(28, 9, HeaderBg, HeaderBg, true), 10, 0); Header.padding = new RectOffset(14, 14, 0, 0);
            TabBar = Box(Solid(TabBg), 0, 0);
            Tab = Text(13, TextDim, TextAnchor.MiddleCenter); Tab.hover.textColor = TextMain; Tab.active.textColor = TextMain;
            TabOn = Text(13, Amber, TextAnchor.MiddleCenter);
            Body = new GUIStyle(); Body.padding = new RectOffset(14, 14, 14, 14);
            Lcd = Box(Round(24, 8, LcdBg, LcdLine), 8, 0); Lcd.padding = new RectOffset(12, 12, 10, 10);
            Request = Box(Round(24, 8, ReqBg, ReqLine), 8, 0); Request.padding = new RectOffset(10, 10, 8, 8);
            Overlay = Box(Round(24, 8, ReqBg, ReqLine), 8, 0); Overlay.padding = new RectOffset(10, 10, 8, 8);
            Overlay.alignment = TextAnchor.MiddleCenter; Overlay.fontSize = 13; Overlay.normal.textColor = TextReq; Overlay.wordWrap = true;

            TitleText = Text(14, TextMain, TextAnchor.MiddleLeft); TitleText.fontStyle = FontStyle.Bold; TitleText.stretchWidth = false;
            Pill = Box(Round(16, 6, PillBg, PillBg), 6, 0); Pill.padding = new RectOffset(8, 8, 2, 2); Pill.fontSize = 12; Pill.normal.textColor = Good; Pill.alignment = TextAnchor.MiddleCenter;
            Pill.stretchWidth = false; Pill.margin = new RectOffset(8, 0, 11, 0); Pill.fixedHeight = 20f;
            Label = Text(13, TextMain, TextAnchor.MiddleLeft);
            Hint = Text(12, TextDim, TextAnchor.MiddleLeft); Hint.wordWrap = true;
            HintRight = Text(12, TextDim, TextAnchor.MiddleRight);
            Number = Text(13, Amber, TextAnchor.MiddleLeft);
            Who = Text(12, TextDim, TextAnchor.MiddleRight);
            StatusOk = Text(12, Good, TextAnchor.MiddleLeft); StatusOk.wordWrap = true;
            StatusBad = Text(12, Bad, TextAnchor.MiddleLeft); StatusBad.wordWrap = true;
            Good1 = Text(12, Good, TextAnchor.MiddleRight);
            ReqTitle = Text(13, TextReq, TextAnchor.MiddleLeft);
            LcdSmall = Text(11, Amber, TextAnchor.MiddleLeft);
            LcdSmallRight = Text(11, Amber, TextAnchor.MiddleRight);
            LcdTitle = Text(15, Amber, TextAnchor.MiddleLeft);

            Button = MakeButton(Btn, BtnHover, BtnDown, new Color32(0x3e, 0x43, 0x4d, 0xff), TextMain);
            ButtonMain = MakeButton(Amber, AmberHover, AmberDown, Amber, TextOnAmber); ButtonMain.fontStyle = FontStyle.Bold;

            TextField = Box(Round(24, 8, Field, new Color32(0x3a, 0x3f, 0x48, 0xff)), 8, 0);
            TextField.padding = new RectOffset(10, 10, 8, 8); TextField.fontSize = 13; TextField.normal.textColor = TextMain; TextField.clipping = TextClipping.Clip;
            TextField.focused.background = Round(24, 8, Field, Amber); TextField.focused.textColor = TextMain;
            TextField.hover.background = TextField.normal.background; TextField.hover.textColor = TextMain;

            HudWindow = new GUIStyle();
            HudNext = Box(Round(24, 8, Panel, Line), 8, 0);
            HudNextText = Text(12, TextMain, TextAnchor.MiddleLeft);
            HudNextTime = Text(12, TextDim, TextAnchor.MiddleRight);

            Slider = new GUIStyle();
            SliderThumb = new GUIStyle(); SliderThumb.normal.background = Round(16, 8, Thumb, Thumb); SliderThumb.fixedWidth = 16f; SliderThumb.fixedHeight = 16f;
            Check = Text(13, TextMain, TextAnchor.MiddleLeft); Check.padding = new RectOffset(24, 0, 0, 0); Check.onNormal.textColor = TextMain; Check.onHover.textColor = TextMain; Check.onActive.textColor = TextMain;
        }

        static GUIStyle Text(int size, Color32 color, TextAnchor anchor)
        {
            GUIStyle s = new GUIStyle();
            s.fontSize = size; s.normal.textColor = color; s.alignment = anchor; s.clipping = TextClipping.Clip;
            return s;
        }

        static GUIStyle Box(Texture2D tex, int corner, int padding)
        {
            GUIStyle s = new GUIStyle();
            s.normal.background = tex; s.onNormal.background = tex; s.border = new RectOffset(corner, corner, corner, corner); s.padding = new RectOffset(padding, padding, padding, padding);
            return s;
        }

        static GUIStyle MakeButton(Color32 fill, Color32 hover, Color32 down, Color32 line, Color32 text)
        {
            GUIStyle s = Box(Round(24, 8, fill, line), 8, 0);
            s.padding = new RectOffset(8, 8, 6, 6); s.fontSize = 13; s.alignment = TextAnchor.MiddleCenter; s.clipping = TextClipping.Clip;
            s.normal.textColor = text; s.hover.textColor = text; s.active.textColor = text;
            s.hover.background = Round(24, 8, hover, line); s.active.background = Round(24, 8, down, line);
            return s;
        }

        static Texture2D New(int w, int h)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static Texture2D Solid(Color32 c)
        {
            Texture2D t = New(2, 2);
            t.SetPixels32(new Color32[] { c, c, c, c }); t.Apply();
            return t;
        }

        // Rounded rectangle with a 1px outline. squareBottom: only the top corners are round (window header).
        static Texture2D Round(int size, int radius, Color32 fill, Color32 line, bool squareBottom = false)
        {
            Texture2D t = New(size, size);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int texY = size - 1 - y;   // row 0 of the texture is the bottom one
                    float d = Dist(x + 0.5f, texY + 0.5f, size, radius);
                    if (squareBottom && texY >= size / 2) d = Mathf.Max(-(x + 0.5f), x + 0.5f - size);
                    px[y * size + x] = Shade(d, fill, line);
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        // Signed distance to the outline of the rounded square (negative = inside).
        static float Dist(float x, float y, int size, int radius)
        {
            float dx = Mathf.Abs(x - size / 2f) - (size / 2f - radius);
            float dy = Mathf.Abs(y - size / 2f) - (size / 2f - radius);
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }

        static Color32 Shade(float d, Color32 fill, Color32 line)
        {
            Color c = Color.Lerp(fill, line, Mathf.Clamp01(d + 1.5f));
            c.a *= Mathf.Clamp01(0.5f - d);
            return c;
        }

        static Texture2D MakeCheck()
        {
            const int size = 16;
            Texture2D t = New(size, size);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(Segment(p, new Vector2(3.5f, 8f), new Vector2(6.5f, 5f)), Segment(p, new Vector2(6.5f, 5f), new Vector2(12.5f, 11.5f)));
                    Color c = Color.Lerp(TextOnAmber, Amber, Mathf.Clamp01(d - 0.6f));
                    c.a *= Mathf.Clamp01(0.5f - Dist(p.x, p.y, size, 4));
                    px[y * size + x] = c;
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * k)).magnitude;
        }
    }
}
