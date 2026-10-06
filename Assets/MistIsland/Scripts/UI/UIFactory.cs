using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MistIsland
{
    /// <summary>uGUI をコードで組み立てるための小物。角丸や円のスプライトもここで生成する。</summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.12f, 0.14f, 0.2f, 0.78f);
        public static readonly Color CardColor = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color TextColor = new Color(0.97f, 0.95f, 0.9f);
        public static readonly Color SubTextColor = new Color(0.78f, 0.8f, 0.86f);
        public static readonly Color Accent = new Color(0.98f, 0.8f, 0.5f);
        public static readonly Color AccentText = new Color(0.22f, 0.18f, 0.14f);
        public static readonly Color ButtonColor = new Color(0.3f, 0.36f, 0.5f, 0.95f);
        public static readonly Color DisabledColor = new Color(0.3f, 0.3f, 0.34f, 0.8f);

        static Font _font;
        static Sprite _rounded;
        static Sprite _circle;

        /// <summary>
        /// 日本語が表示できるフォント。Resources/MistIsland/Fonts/UIFont に .ttf/.otf を置けばそれを使う。
        /// なければ OS の日本語フォント、最後に Unity 標準フォントを使う。
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("MistIsland/Fonts/UIFont");
                if (_font == null)
                {
                    string[] candidates = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP", "Noto Sans JP", "Droid Sans Japanese" };
                    var installed = new System.Collections.Generic.HashSet<string>(Font.GetOSInstalledFontNames());
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var c in candidates)
                        if (installed.Contains(c)) names.Add(c);
                    if (names.Count > 0) _font = Font.CreateDynamicFontFromOSFont(names.ToArray(), 32);
                }
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null) _rounded = MakeRounded(64, 22);
                return _rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = MakeRounded(128, 64);
                return _circle;
            }
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - (half - radius));
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - (half - radius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = Mathf.Min(radius + 2, half - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>アンカー・ピボット・位置・大きさをまとめて設定する。</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button MakeButton(Transform parent, string label, int fontSize, UnityAction onClick, Color? background = null, Color? textColor = null)
        {
            Image img = Panel(parent, "Button", background ?? ButtonColor);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            Text t = Label(img.transform, label, fontSize, textColor ?? TextColor, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 8, 8, 4, 4);
            return btn;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        /// <summary>横長のゲージ。戻り値の RectTransform の anchorMax.x で量を表す。</summary>
        public static RectTransform Bar(Transform parent, string name, Color back, Color fill, out RectTransform root)
        {
            Image bg = Panel(parent, name, back);
            root = bg.rectTransform;
            Image f = Panel(bg.transform, "Fill", fill);
            var rt = f.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
            return rt;
        }

        public static void SetBar(RectTransform fill, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            fill.anchorMax = new Vector2(ratio, 1f);
            fill.gameObject.SetActive(ratio > 0.001f);
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1f, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            if (preferredHeight > 0) le.minHeight = preferredHeight;
            return le;
        }
        // ---- 一覧画面の部品 ----

        public struct RowButton
        {
            public string label;
            public bool enabled;
            public UnityAction onClick;
            public float width;

            public RowButton(string label, bool enabled, UnityAction onClick, float width = 260f)
            {
                this.label = label;
                this.enabled = enabled;
                this.onClick = onClick;
                this.width = width;
            }
        }

        /// <summary>縦に並べるリストの見出しや説明文。</summary>
        public static Text Paragraph(Transform parent, string text, int size = 34)
        {
            return Label(parent, text, size, TextColor, TextAnchor.UpperLeft);
        }

        /// <summary>左に説明、右にボタン（0〜複数）を並べた1行。</summary>
        public static Image Row(Transform parent, string text, float height, params RowButton[] buttons)
        {
            Image row = Panel(parent, "Row", CardColor);
            Layout(row, height);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(28, 18, 12, 12);
            h.spacing = 14;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            Text t = Label(row.transform, text, 32, TextColor);
            Layout(t, -1f, 0f, 1f);

            foreach (var spec in buttons)
            {
                if (spec.label == null) continue;
                Button b = MakeButton(row.transform, spec.label, 28, spec.onClick,
                    spec.enabled ? Accent : DisabledColor, spec.enabled ? AccentText : SubTextColor);
                b.interactable = spec.enabled;
                Layout(b, -1f, spec.width);
            }
            return row;
        }

        public static string Cost(int coins, int materials)
        {
            string s = coins + "コイン";
            if (materials > 0) s += " " + materials + "素材";
            return s;
        }

        /// <summary>レイアウトに残らないよう、非表示にしてから消す。</summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        /// <summary>縦に並べる入れ物（VerticalLayoutGroup 付き）。</summary>
        public static RectTransform VerticalList(Transform parent, string name, float spacing = 16f)
        {
            RectTransform rt = Rect(name, parent);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            return rt;
        }
    }
}
