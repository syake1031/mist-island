using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MistIsland
{
    /// <summary>
    /// 縦画面の UI。上に時間帯・所持品・レベル、下に移動スティックと攻撃ボタン、
    /// 近くの建物を調べるボタン、メニュー・建設・ダイアログのウィンドウを持つ。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        GameManager _gm;
        Canvas _canvas;
        RectTransform _safe;
        Rect _lastSafeArea;

        Text _phaseText;
        Text _timeText;
        RectTransform _phaseBar;
        Image _phaseBarImage;
        Text _coinText;
        Text _materialText;
        Text _levelText;
        RectTransform _xpBar;
        RectTransform _hpBar;
        Text _hpText;
        GameObject _hallRoot;
        RectTransform _hallBar;
        Text _respawnText;

        Button _interactButton;
        Text _interactInfo;
        Interaction _interaction;

        GameObject _toastRoot;
        Text _toastText;
        float _toastTimer;

        RectTransform _floatLayer;
        readonly List<Floating> _floats = new List<Floating>();
        readonly Stack<Text> _floatPool = new Stack<Text>();

        GameObject _modal;
        Text _modalTitle;
        RectTransform _modalTabs;
        RectTransform _modalContent;
        Button _modalClose;
        System.Action _modalRefresh;
        CampScreen _camp;

        class Floating
        {
            public Text text;
            public Vector3 world;
            public float age;
        }

        static readonly Color MorningColor = new Color(0.98f, 0.78f, 0.6f);
        static readonly Color DayColor = new Color(0.72f, 0.88f, 0.98f);
        static readonly Color NightColor = new Color(0.62f, 0.6f, 0.95f);

        public bool IsModalOpen { get { return _modal != null && _modal.activeSelf; } }

        public void Initialize(GameManager gm)
        {
            _gm = gm;
            BuildCanvas();
            BuildTop();
            BuildBottom();
            BuildModal();
            _camp = UIFactory.Stretch(UIFactory.Rect("BaseCamp", transform)).gameObject.AddComponent<CampScreen>();
            _camp.Initialize(_gm, this);
            BuildToast();
            _gm.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_gm != null) _gm.Changed -= Refresh;
        }

        // ================= 組み立て =================

        void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            _safe = UIFactory.Stretch(UIFactory.Rect("SafeArea", transform));
            ApplySafeArea();

            // 一番後ろに全画面のタッチ受付を置く
            var touch = UIFactory.Stretch(UIFactory.Rect("TouchArea", _safe));
            var touchImage = touch.gameObject.AddComponent<Image>();
            touchImage.color = new Color(0, 0, 0, 0);
            var stickBase = UIFactory.Place(UIFactory.Panel(_safe, "StickBase", new Color(1, 1, 1, 0.18f), UIFactory.Circle).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            stickBase.GetComponent<Image>().raycastTarget = false;
            var knob = UIFactory.Place(UIFactory.Panel(stickBase, "Knob", new Color(1, 1, 1, 0.55f), UIFactory.Circle).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            knob.GetComponent<Image>().raycastTarget = false;
            touch.gameObject.AddComponent<TouchArea>().Initialize(stickBase, knob);

            _floatLayer = UIFactory.Stretch(UIFactory.Rect("FloatingTexts", _safe));
        }

        void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            if (area == _lastSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            _lastSafeArea = area;
            _safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safe.offsetMin = Vector2.zero;
            _safe.offsetMax = Vector2.zero;
        }

        void BuildTop()
        {
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);

            // 時間帯
            var phaseCard = UIFactory.Place(UIFactory.Panel(_safe, "PhaseCard", UIFactory.PanelColor).rectTransform, topLeft, topLeft, new Vector2(24, -24), new Vector2(560, 150));
            _phaseText = UIFactory.Label(phaseCard, "", 46, UIFactory.TextColor);
            UIFactory.Place(_phaseText.rectTransform, topLeft, topLeft, new Vector2(28, -12), new Vector2(340, 70));
            _timeText = UIFactory.Label(phaseCard, "", 34, UIFactory.SubTextColor, TextAnchor.MiddleRight);
            UIFactory.Place(_timeText.rectTransform, topRight, topRight, new Vector2(-28, -12), new Vector2(200, 70));
            RectTransform phaseBarRoot;
            _phaseBar = UIFactory.Bar(phaseCard, "PhaseBar", new Color(1, 1, 1, 0.12f), MorningColor, out phaseBarRoot);
            UIFactory.Place(phaseBarRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 22), new Vector2(512, 34));
            _phaseBarImage = _phaseBar.GetComponent<Image>();

            // 所持品
            var walletCard = UIFactory.Place(UIFactory.Panel(_safe, "WalletCard", UIFactory.PanelColor).rectTransform, topRight, topRight, new Vector2(-24, -24), new Vector2(430, 150));
            _coinText = UIFactory.Label(walletCard, "", 40, new Color(1f, 0.87f, 0.45f));
            UIFactory.Place(_coinText.rectTransform, topLeft, topLeft, new Vector2(28, -8), new Vector2(380, 66));
            _materialText = UIFactory.Label(walletCard, "", 40, new Color(0.7f, 0.87f, 1f));
            UIFactory.Place(_materialText.rectTransform, topLeft, topLeft, new Vector2(28, -74), new Vector2(380, 66));

            // レベル・体力
            var statusCard = UIFactory.Place(UIFactory.Panel(_safe, "StatusCard", UIFactory.PanelColor).rectTransform, topLeft, topLeft, new Vector2(24, -190), new Vector2(560, 170));
            _levelText = UIFactory.Label(statusCard, "", 34, UIFactory.TextColor);
            UIFactory.Place(_levelText.rectTransform, topLeft, topLeft, new Vector2(28, -8), new Vector2(510, 56));
            RectTransform xpRoot;
            _xpBar = UIFactory.Bar(statusCard, "XpBar", new Color(1, 1, 1, 0.12f), new Color(0.98f, 0.85f, 0.5f), out xpRoot);
            UIFactory.Place(xpRoot, topLeft, topLeft, new Vector2(24, -66), new Vector2(512, 26));
            RectTransform hpRoot;
            _hpBar = UIFactory.Bar(statusCard, "HpBar", new Color(1, 1, 1, 0.12f), new Color(0.55f, 0.85f, 0.55f), out hpRoot);
            UIFactory.Place(hpRoot, topLeft, topLeft, new Vector2(24, -104), new Vector2(512, 46));
            _hpText = UIFactory.Label(hpRoot, "", 28, UIFactory.TextColor, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_hpText.rectTransform);

            // メニュー
            var menu = UIFactory.MakeButton(_safe, "島・町", 38, OpenMenu, UIFactory.Accent, UIFactory.AccentText);
            UIFactory.Place((RectTransform)menu.transform, topRight, topRight, new Vector2(-24, -194), new Vector2(260, 110));

            // 夜だけ出る拠点の耐久
            var hallCard = UIFactory.Place(UIFactory.Panel(_safe, "HallCard", UIFactory.PanelColor).rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -380), new Vector2(640, 70));
            _hallRoot = hallCard.gameObject;
            var hallLabel = UIFactory.Label(hallCard, "拠点", 30, UIFactory.TextColor);
            UIFactory.Place(hallLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24, 0), new Vector2(100, 60));
            RectTransform hallBarRoot;
            _hallBar = UIFactory.Bar(hallCard, "HallBar", new Color(1, 1, 1, 0.12f), new Color(0.95f, 0.6f, 0.5f), out hallBarRoot);
            UIFactory.Place(hallBarRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120, 0), new Vector2(496, 36));

            _respawnText = UIFactory.Label(_safe, "", 52, UIFactory.TextColor, TextAnchor.MiddleCenter);
            UIFactory.Place(_respawnText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(900, 120));
        }

        void BuildBottom()
        {
            var bottomRight = new Vector2(1f, 0f);


            _interactButton = UIFactory.MakeButton(_safe, "建てる", 38, OnInteract, UIFactory.Accent, UIFactory.AccentText);
            UIFactory.Place((RectTransform)_interactButton.transform, bottomRight, bottomRight, new Vector2(-60, 200), new Vector2(380, 130));

            _interactInfo = UIFactory.Label(_safe, "", 30, UIFactory.TextColor, TextAnchor.LowerRight);
            UIFactory.Place(_interactInfo.rectTransform, bottomRight, bottomRight, new Vector2(-64, 340), new Vector2(560, 160));

            var hint = UIFactory.Label(_safe, "左ドラッグ：移動　右ドラッグ：カメラ回転　右長押し：溜め攻撃", 26, new Color(1, 1, 1, 0.55f), TextAnchor.LowerLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32, 28), new Vector2(1000, 50));
        }

        void BuildToast()
        {
            // ウィンドウの上にも出るよう、セーフエリアではなくキャンバス直下に置く
            var toast = UIFactory.Place(UIFactory.Panel(transform, "Toast", new Color(0.1f, 0.12f, 0.18f, 0.82f)).rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -470), new Vector2(900, 120));
            _toastRoot = toast.gameObject;
            _toastText = UIFactory.Label(toast, "", 34, UIFactory.TextColor, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_toastText.rectTransform, 24, 24, 8, 8);
            var fitter = toast.gameObject.AddComponent<VerticalLayoutGroup>();
            fitter.padding = new RectOffset(28, 28, 20, 20);
            fitter.childControlHeight = true;
            fitter.childControlWidth = true;
            toast.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _toastRoot.SetActive(false);
        }

        void BuildModal()
        {
            var dim = UIFactory.Stretch(UIFactory.Rect("Modal", transform));
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0.05f, 0.06f, 0.1f, 0.55f);
            _modal = dim.gameObject;

            var window = UIFactory.Place(UIFactory.Panel(dim, "Window", new Color(0.14f, 0.16f, 0.23f, 0.97f)).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 1560));

            _modalTitle = UIFactory.Label(window, "", 48, UIFactory.Accent, TextAnchor.MiddleCenter);
            UIFactory.Place(_modalTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(920, 90));

            _modalTabs = UIFactory.Place(UIFactory.Rect("Tabs", window), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -124), new Vector2(920, 100));
            var tabsLayout = _modalTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 16;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            _modalContent = UIFactory.Rect("Content", window);
            var layout = _modalContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            _modalClose = UIFactory.MakeButton(window, "閉じる", 40, CloseModal);
            UIFactory.Place((RectTransform)_modalClose.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(520, 120));

            _modal.SetActive(false);
        }

        // ================= 毎フレーム =================

        void Update()
        {
            if (_gm == null) return;
            ApplySafeArea();
            UpdateClock();
            UpdatePlayer();
            UpdateInteraction();
            UpdateToast();
            UpdateFloats();

            if (_camp.IsOpen)
            {
                if (InputBridge.MenuPressed) _camp.Close();
                return;
            }
            if (InputBridge.MenuPressed)
            {
                if (IsModalOpen) CloseModal();
                else OpenMenu();
            }
            if (InputBridge.InteractPressed && !IsModalOpen && _interactButton.gameObject.activeSelf) OnInteract();
        }

        void UpdateClock()
        {
            DayCycle c = _gm.Clock;
            _phaseText.text = c.Day + "日目・" + Names.Of(c.Phase);
            int remain = Mathf.CeilToInt(c.PhaseRemaining);
            _timeText.text = (remain / 60) + ":" + (remain % 60).ToString("00");
            UIFactory.SetBar(_phaseBar, 1f - c.PhaseProgress);
            _phaseBarImage.color = c.Phase == Phase.Morning ? MorningColor : c.Phase == Phase.Day ? DayColor : NightColor;

            bool showHall = c.IsNight || !_gm.Town.Hall.IsAlive || _gm.Town.Hall.Current < _gm.Town.Hall.Max;
            if (_hallRoot.activeSelf != showHall) _hallRoot.SetActive(showHall);
            if (showHall) UIFactory.SetBar(_hallBar, _gm.Town.Hall.Ratio);
        }

        void UpdatePlayer()
        {
            Health h = _gm.Player.Health;
            UIFactory.SetBar(_hpBar, h.Ratio);
            _hpText.text = "体力 " + Mathf.CeilToInt(h.Current) + " / " + Mathf.CeilToInt(h.Max);
            _respawnText.text = h.IsAlive ? "" : "復活まで " + Mathf.CeilToInt(_gm.Player.RespawnRemaining);
        }

        /// <summary>所持品やレベルなど、変化したときだけ更新すればよい表示。</summary>
        void Refresh()
        {
            SaveData d = _gm.Data;
            _coinText.text = "コイン " + d.coins;
            _materialText.text = "素材 " + d.materials;
            JobDef job = _gm.Config.Job(d.jobIndex);
            if (_gm.IsMaxLevel)
            {
                _levelText.text = "Lv" + d.level + "（最大）　" + job.name;
                UIFactory.SetBar(_xpBar, 1f);
            }
            else
            {
                _levelText.text = "Lv" + d.level + "　" + job.name + "　<size=26>EXP " + d.xp + "/" + _gm.XpToNext + "</size>";
                UIFactory.SetBar(_xpBar, d.xp / (float)Mathf.Max(1, _gm.XpToNext));
            }
            if (IsModalOpen && _modalRefresh != null) _modalRefresh();
        }

        void UpdateInteraction()
        {
            bool alive = _gm.Player.Health.IsAlive;
            _interaction = alive ? _gm.Town.FindInteraction(_gm.Player.transform.position, _gm.Config.interactRadius) : new Interaction();
            bool show = _interaction.valid && !IsModalOpen && !_camp.IsOpen;
            if (_interactButton.gameObject.activeSelf != show) _interactButton.gameObject.SetActive(show);

            string info = "";
            if (show)
            {
                if (_interaction.isHall)
                {
                    UIFactory.SetButtonLabel(_interactButton, "ベースキャンプ");
                    info = "ジョブ・装備を変える";
                }
                else if (_interaction.building != null)
                {
                    Building b = _interaction.building;
                    UIFactory.SetButtonLabel(_interactButton, b.Def.name + " Lv" + b.Level);
                    if (b.Ruined) info = "壊れている（朝に直る）";
                    else if (b.Def.IsFacility) info = (b.Def.producesMaterials ? "素材 " : "コイン ") + Mathf.FloorToInt(b.Stored) + " / " + Mathf.FloorToInt(b.Capacity);
                    else info = "耐久 " + Mathf.CeilToInt(b.Health.Current) + " / " + Mathf.CeilToInt(b.Health.Max);
                }
                else
                {
                    UIFactory.SetButtonLabel(_interactButton, "建てる");
                    info = "空き地";
                }
            }
            if (_interactInfo.text != info) _interactInfo.text = info;
        }

        void OnInteract()
        {
            if (!_interaction.valid) return;
            if (_interaction.isHall) _camp.Open();
            else if (_interaction.building != null) OpenBuilding(_interaction.building);
            else OpenBuild(_interaction.slot);
        }

        // ================= トースト・浮かぶ文字 =================

        public void Toast(string message, float seconds)
        {
            if (_toastText == null) return;
            _toastText.text = message;
            _toastTimer = seconds;
            _toastRoot.SetActive(true);
        }

        void UpdateToast()
        {
            if (_toastTimer <= 0f) return;
            _toastTimer -= Time.unscaledDeltaTime;
            if (_toastTimer <= 0f) _toastRoot.SetActive(false);
        }

        public void FloatingText(Vector3 world, string text, Color color)
        {
            Text t = _floatPool.Count > 0 ? _floatPool.Pop() : null;
            if (t == null)
            {
                t = UIFactory.Label(_floatLayer, "", 34, color, TextAnchor.MiddleCenter);
                t.rectTransform.sizeDelta = new Vector2(400, 60);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = color;
            _floats.Add(new Floating { text = t, world = world + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f)) });
        }

        void UpdateFloats()
        {
            Camera cam = _gm.Rig != null ? _gm.Rig.Camera : null;
            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                Floating f = _floats[i];
                f.age += Time.deltaTime;
                if (f.age > 1.1f || cam == null)
                {
                    f.text.gameObject.SetActive(false);
                    _floatPool.Push(f.text);
                    _floats.RemoveAt(i);
                    continue;
                }
                Vector3 screen = cam.WorldToScreenPoint(f.world + Vector3.up * f.age * 1.2f);
                if (screen.z < 0f)
                {
                    f.text.gameObject.SetActive(false);
                    continue;
                }
                f.text.gameObject.SetActive(true);
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_floatLayer, screen, null, out local);
                f.text.rectTransform.anchoredPosition = local;
                Color c = f.text.color;
                c.a = Mathf.Clamp01((1.1f - f.age) / 0.4f);
                f.text.color = c;
            }
        }

        // ================= ウィンドウ =================

        void OpenModal(string title, bool tabs, System.Action fill)
        {
            InputBridge.JoystickValue = Vector2.zero;
            InputBridge.ChargeHeld = false;
            _modal.SetActive(true);
            _modal.transform.SetAsLastSibling();
            _toastRoot.transform.SetAsLastSibling();
            _modalTitle.text = title;
            _modalTabs.gameObject.SetActive(tabs);
            var rt = _modalContent;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(40, 180);
            rt.offsetMax = new Vector2(-40, tabs ? -250 : -140);
            _modalRefresh = fill;
            fill();
        }

        public void CloseModal()
        {
            _modal.SetActive(false);
            _modalRefresh = null;
        }

        void ClearContent()
        {
            ClearChildren(_modalContent);
        }

        Text Paragraph(string text, int size = 34)
        {
            return UIFactory.Paragraph(_modalContent, text, size);
        }

        void Row(string text, string buttonLabel, bool enabled, UnityAction onClick, float height = 150f)
        {
            UIFactory.Row(_modalContent, text, height, new UIFactory.RowButton(buttonLabel, enabled, onClick, 300f));
        }

        static string Cost(int coins, int materials)
        {
            return UIFactory.Cost(coins, materials);
        }

        static void ClearChildren(Transform parent)
        {
            UIFactory.ClearChildren(parent);
        }

        void Act(bool ok, string error)
        {
            if (!ok && !string.IsNullOrEmpty(error)) Toast(error, 2f);
        }

        public void ShowDialog(string title, string body)
        {
            OpenModal(title, false, () =>
            {
                ClearContent();
                Paragraph(body, 36);
            });
        }

        void ShowConfirm(string title, string body, string yesLabel, UnityAction onYes)
        {
            OpenModal(title, false, () =>
            {
                ClearContent();
                Paragraph(body, 36);
                Row("", yesLabel, true, () =>
                {
                    CloseModal();
                    onYes();
                });
            });
        }

        // ---- メニュー ----

        void OpenMenu()
        {
            OpenModal("島", false, () =>
            {
                ClearContent();
                FillIsland();
            });
        }

        void FillIsland()
        {
            SaveData d = _gm.Data;
            GameConfig cfg = _gm.Config;

            {
                string text = "<b>島の拡張</b>　段階 " + d.expansion + " / " + cfg.MaxExpansion + "\n<size=28>島が大きくなり、新しい空き地が増える</size>";
                string label;
                bool enabled = false;
                if (_gm.IsFullyExpanded) label = "最大";
                else if (d.level < _gm.NextExpansionLevel) label = "Lv" + _gm.NextExpansionLevel + "で開放";
                else
                {
                    int c, m;
                    _gm.ExpansionCost(out c, out m);
                    label = "広げる\n<size=24>" + Cost(c, m) + "</size>";
                    enabled = _gm.IsPrepTime && _gm.CanAfford(c, m);
                }
                Row(text, label, enabled, () =>
                {
                    string err;
                    Act(_gm.TryExpand(out err), err);
                });
            }

            float coinPerMin = 0f, matPerMin = 0f;
            foreach (var b in _gm.Town.Buildings)
            {
                if (!b.Def.IsFacility) continue;
                if (b.Def.producesMaterials) matPerMin += b.IncomePerSecond * 60f;
                else coinPerMin += b.IncomePerSecond * 60f;
            }
            float tonight = Formulas.NightStrength(_gm.Clock.Day, cfg.offlineEnemyBaseStrength, cfg.offlineEnemyGrowthPerDay);
            float defense = _gm.Town.DefensePower;
            Paragraph("<b>町の様子</b>\n" +
                      "施設の収入：コイン " + coinPerMin.ToString("0") + "/分" + (matPerMin > 0f ? "、素材 " + matPerMin.ToString("0.#") + "/分" : "") + "\n" +
                      "防衛力 " + Mathf.RoundToInt(defense) + "　／　放置中の今夜の敵の強さ " + Mathf.RoundToInt(tonight) + "\n" +
                      "<size=28>" + (defense >= tonight ? "今夜は放置しても持ちこたえられそう" : "放置すると今夜は防衛装置が壊されそう。見張り塔や柵を強化しよう") + "</size>", 32);

            Row("<b>次の時間帯へ進める</b>\n<size=28>テスト用</size>", "進める", true, () =>
            {
                _gm.DebugSkipPhase();
                CloseModal();
            }, 130f);
            Row("<b>最初からやり直す</b>\n<size=28>セーブデータを消します</size>", "リセット", true, () =>
            {
                ShowConfirm("最初からやり直す", "セーブデータを消して最初から始めます。よろしいですか？", "消して始める", _gm.ResetProgress);
            }, 130f);
        }

        // ---- 建設 ----

        void OpenBuild(SlotInfo slot)
        {
            OpenModal("建てる", false, () =>
            {
                ClearContent();
                if (!_gm.IsPrepTime) Paragraph("夜は建てられません。朝を待とう", 32);
                foreach (var def in _gm.Config.buildings)
                {
                    BuildingDef bd = def;
                    bool unlocked = _gm.Data.level >= def.unlockLevel;
                    int c = TownManager.BuildCoinCost(def), m = TownManager.BuildMaterialCost(def);
                    string kind = def.IsDefense ? "防衛装置" : "施設";
                    string text = "<b>" + def.name + "</b>　<size=26>" + kind + "</size>\n<size=28>" + def.description + "</size>";
                    string label = unlocked ? "建てる\n<size=24>" + Cost(c, m) + "</size>" : "Lv" + def.unlockLevel + "で開放";
                    Row(text, label, unlocked && _gm.IsPrepTime && _gm.CanAfford(c, m), () =>
                    {
                        string err;
                        bool ok = _gm.Town.TryBuild(slot, bd, out err);
                        Act(ok, err);
                        if (ok) CloseModal();
                    });
                }
            });
        }

        void OpenBuilding(Building b)
        {
            OpenModal(b.Def.name, false, () =>
            {
                ClearContent();
                if (b == null) return;
                string text = "<b>" + b.Def.name + " Lv" + b.Level + "</b>　<size=28>" + b.Def.description + "</size>\n";
                text += "耐久 " + Mathf.CeilToInt(b.Health.Current) + " / " + Mathf.CeilToInt(b.Health.Max) + (b.Ruined ? "（壊れている・朝に直る）" : "") + "\n";
                if (b.Def.IsFacility)
                {
                    string unit = b.Def.producesMaterials ? "素材" : "コイン";
                    text += "収入 " + (b.IncomePerSecond * 60f).ToString("0.#") + " " + unit + "/分　上限 " + Mathf.FloorToInt(b.Capacity) + "\n";
                    text += "貯まっている " + unit + "：" + Mathf.FloorToInt(b.Stored) + "（近づくと受け取れる）";
                }
                if (b.Def.IsDefense)
                {
                    text += "防衛力 " + Mathf.RoundToInt(b.DefensePower);
                    if (b.Def.damage > 0f) text += "　射程 " + b.Range.ToString("0.#") + "　攻撃 " + b.Damage.ToString("0.#");
                }
                Paragraph(text, 32);

                if (b.IsMaxLevel)
                {
                    Row("これ以上強化できません", null, false, null);
                }
                else
                {
                    int c = b.NextCoinCost, m = b.NextMaterialCost;
                    Row("<b>Lv" + (b.Level + 1) + " に強化</b>\n<size=28>" + (b.Def.IsFacility ? "収入と上限が増える" : "耐久と防衛力が上がる") + "</size>",
                        "強化\n<size=24>" + Cost(c, m) + "</size>", _gm.IsPrepTime && _gm.CanAfford(c, m), () =>
                        {
                            string err;
                            Act(_gm.Town.TryUpgrade(b, out err), err);
                        });
                }
                if (!_gm.IsPrepTime) Paragraph("<size=28>夜のあいだは強化できません</size>", 28);
            });
        }
    }
}
