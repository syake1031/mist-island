using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MistIsland
{
    /// <summary>
    /// ベースキャンプ画面（モンハンのベースキャンプのような場所）。
    /// 拠点に近づいて入ると画面が切り替わり、ジョブ・武器・防具を変えたり作ったり強化したりできる。
    /// 中にいる間はゲームの時間が止まる。見た目は仮。
    /// </summary>
    public class CampScreen : MonoBehaviour
    {
        GameManager _gm;
        Hud _hud;
        GameObject _root;
        CanvasGroup _fade;
        Text _header;
        RectTransform _tabs;
        RectTransform _content;
        int _tab;
        bool _transitioning;

        static readonly string[] TabNames = { "ジョブ", "武器", "防具" };
        static readonly Color Background = new Color(0.17f, 0.15f, 0.14f, 1f);

        public bool IsOpen { get { return _root.activeSelf; } }

        public void Initialize(GameManager gm, Hud hud)
        {
            _gm = gm;
            _hud = hud;
            Build();
            _gm.Changed += Refresh;
        }

        void OnDestroy()
        {
            if (_gm != null) _gm.Changed -= Refresh;
        }

        void Build()
        {
            var rootImage = UIFactory.Panel(transform, "Screen", Background, null);
            rootImage.sprite = null;
            rootImage.type = Image.Type.Simple;
            UIFactory.Stretch(rootImage.rectTransform);
            _root = rootImage.gameObject;

            var title = UIFactory.Label(_root.transform, "ベースキャンプ", 56, UIFactory.Accent, TextAnchor.MiddleCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1000, 100));

            _header = UIFactory.Label(_root.transform, "", 32, UIFactory.TextColor, TextAnchor.UpperCenter);
            UIFactory.Place(_header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1000, 130));

            _tabs = UIFactory.Place(UIFactory.Rect("Tabs", _root.transform), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -340), new Vector2(980, 110));
            var tabsLayout = _tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 16;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;

            _content = UIFactory.VerticalList(_root.transform, "Content");
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(50, 230);
            _content.offsetMax = new Vector2(-50, -480);

            var leave = UIFactory.MakeButton(_root.transform, "出発する", 44, Close, UIFactory.Accent, UIFactory.AccentText);
            UIFactory.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(620, 140));

            // 画面切り替え用の暗転
            var fade = UIFactory.Panel(transform, "Fade", Color.black, null);
            fade.sprite = null;
            fade.type = Image.Type.Simple;
            UIFactory.Stretch(fade.rectTransform);
            _fade = fade.gameObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;

            _root.SetActive(false);
        }

        // ---- 出入り ----

        public void Open()
        {
            if (IsOpen || _transitioning) return;
            StartCoroutine(Transition(true));
        }

        public void Close()
        {
            if (!IsOpen || _transitioning) return;
            StartCoroutine(Transition(false));
        }

        IEnumerator Transition(bool open)
        {
            _transitioning = true;
            _fade.blocksRaycasts = true;
            yield return FadeTo(1f);

            if (open)
            {
                _gm.EnterCamp();
                _hud.CloseModal();
                _root.SetActive(true);
                Refresh();
            }
            else
            {
                _root.SetActive(false);
                _gm.LeaveCamp();
            }

            yield return FadeTo(0f);
            _fade.blocksRaycasts = false;
            _transitioning = false;
        }

        IEnumerator FadeTo(float target)
        {
            const float duration = 0.25f;
            float start = _fade.alpha;
            float t = 0f;
            while (t < duration)
            {
                // ゲーム内の時間は止まるので、実時間で進める
                t += Time.unscaledDeltaTime;
                _fade.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            _fade.alpha = target;
        }

        // ---- 中身 ----

        void Refresh()
        {
            if (!IsOpen) return;
            SaveData d = _gm.Data;
            PlayerStats s = _gm.CurrentStats;
            _header.text = "コイン " + d.coins + "　素材 " + d.materials + "\n" +
                           "Lv" + d.level + "　" + s.job.name + "　体力 " + Mathf.RoundToInt(s.maxHp) +
                           "　攻撃 " + s.damage.ToString("0.0") + "　被ダメ軽減 " + Mathf.RoundToInt(s.damageReduction * 100f) + "%";

            UIFactory.ClearChildren(_tabs);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                bool active = i == _tab;
                UIFactory.MakeButton(_tabs, TabNames[i], 38, () => { _tab = index; Refresh(); },
                    active ? UIFactory.Accent : UIFactory.ButtonColor, active ? UIFactory.AccentText : UIFactory.TextColor);
            }

            UIFactory.ClearChildren(_content);
            switch (_tab)
            {
                case 0: FillJobs(); break;
                case 1: FillWeapons(); break;
                default: FillArmors(); break;
            }
        }

        void Act(bool ok, string error)
        {
            if (!ok && !string.IsNullOrEmpty(error)) _hud.Toast(error, 2f);
        }

        static string RangeName(WeaponType w)
        {
            switch (w)
            {
                case WeaponType.Sword: return "近距離";
                case WeaponType.Spear: return "中距離";
                default: return "遠距離";
            }
        }

        void FillJobs()
        {
            UIFactory.Paragraph(_content, "ジョブを変えると、使える武器と能力が変わる", 30);
            for (int i = 0; i < _gm.Config.jobs.Length; i++)
            {
                int index = i;
                JobDef j = _gm.Config.jobs[i];
                bool current = _gm.Data.jobIndex == i;
                bool unlocked = _gm.IsJobUnlocked(j);
                string text = "<b>" + j.name + "</b>　武器：" + Names.Of(j.weapon) + "（" + RangeName(j.weapon) + "）\n" +
                              "<size=28>体力×" + j.hpMultiplier.ToString("0.0#") + "　速さ×" + j.speedMultiplier.ToString("0.0#") + "</size>";
                string label = current ? "使用中" : unlocked ? "このジョブにする" : "Lv" + j.unlockLevel + "で開放";
                UIFactory.Row(_content, text, 150f, new UIFactory.RowButton(label, !current && unlocked, () =>
                {
                    string err;
                    Act(_gm.TryChangeJob(index, out err), err);
                }, 300f));
            }
        }

        void FillWeapons()
        {
            SaveData d = _gm.Data;
            JobDef job = _gm.Config.Job(d.jobIndex);
            UIFactory.Paragraph(_content, job.name + "の武器（" + Names.Of(job.weapon) + "）\n<size=26>ほかの種類の武器は、ジョブを変えると使える</size>", 30);

            string equipped = d.EquippedWeaponId(job.weapon);
            foreach (var def in _gm.Config.weaponItems)
            {
                if (def.type != job.weapon) continue;
                WeaponItemDef item = def;
                OwnedItem owned = d.FindWeapon(def.id);
                if (owned != null)
                {
                    float dmg = def.damage * (1f + _gm.Config.weaponDamagePerLevel * (owned.level - 1));
                    string text = "<b>" + def.name + " +" + (owned.level - 1) + "</b>\n<size=28>攻撃力 " + dmg.ToString("0.0") + "</size>";
                    bool isEquipped = equipped == def.id;
                    bool max = owned.level >= _gm.Config.maxEquipLevel;
                    int c, m;
                    _gm.ItemUpgradeCost(def.coinCost, def.materialCost, owned.level, out c, out m);
                    UIFactory.Row(_content, text, 150f,
                        new UIFactory.RowButton(isEquipped ? "装備中" : "装備する", !isEquipped, () => _gm.EquipWeapon(item), 200f),
                        new UIFactory.RowButton(max ? "最大" : "強化\n<size=22>" + UIFactory.Cost(c, m) + "</size>", !max && _gm.CanAfford(c, m), () =>
                        {
                            string err;
                            Act(_gm.TryUpgradeWeapon(item, out err), err);
                        }, 250f));
                }
                else
                {
                    bool unlocked = d.level >= def.unlockLevel;
                    string text = "<b>" + def.name + "</b>（未所持）\n<size=28>攻撃力 " + def.damage.ToString("0.0") + "</size>";
                    string label = unlocked ? "作る\n<size=22>" + UIFactory.Cost(def.coinCost, def.materialCost) + "</size>" : "Lv" + def.unlockLevel + "で作れる";
                    UIFactory.Row(_content, text, 150f, new UIFactory.RowButton(label, unlocked && _gm.CanAfford(def.coinCost, def.materialCost), () =>
                    {
                        string err;
                        Act(_gm.TryCraftWeapon(item, out err), err);
                    }, 300f));
                }
            }
        }

        void FillArmors()
        {
            SaveData d = _gm.Data;
            UIFactory.Paragraph(_content, "防具はどのジョブでも使える", 30);
            foreach (var def in _gm.Config.armorItems)
            {
                ArmorItemDef item = def;
                OwnedItem owned = d.FindArmor(def.id);
                if (owned != null)
                {
                    float scale = 1f + _gm.Config.armorPerLevel * (owned.level - 1);
                    string text = "<b>" + def.name + " +" + (owned.level - 1) + "</b>\n<size=28>被ダメ軽減 " + Mathf.RoundToInt(def.damageReduction * scale * 100f) +
                                  "%　体力 +" + Mathf.RoundToInt(def.bonusHp * scale) + "</size>";
                    bool isEquipped = d.equippedArmor == def.id;
                    bool max = owned.level >= _gm.Config.maxEquipLevel;
                    int c, m;
                    _gm.ItemUpgradeCost(def.coinCost, def.materialCost, owned.level, out c, out m);
                    UIFactory.Row(_content, text, 150f,
                        new UIFactory.RowButton(isEquipped ? "装備中" : "装備する", !isEquipped, () => _gm.EquipArmor(item), 200f),
                        new UIFactory.RowButton(max ? "最大" : "強化\n<size=22>" + UIFactory.Cost(c, m) + "</size>", !max && _gm.CanAfford(c, m), () =>
                        {
                            string err;
                            Act(_gm.TryUpgradeArmor(item, out err), err);
                        }, 250f));
                }
                else
                {
                    bool unlocked = d.level >= def.unlockLevel;
                    string text = "<b>" + def.name + "</b>（未所持）\n<size=28>被ダメ軽減 " + Mathf.RoundToInt(def.damageReduction * 100f) + "%　体力 +" + Mathf.RoundToInt(def.bonusHp) + "</size>";
                    string label = unlocked ? "作る\n<size=22>" + UIFactory.Cost(def.coinCost, def.materialCost) + "</size>" : "Lv" + def.unlockLevel + "で作れる";
                    UIFactory.Row(_content, text, 150f, new UIFactory.RowButton(label, unlocked && _gm.CanAfford(def.coinCost, def.materialCost), () =>
                    {
                        string err;
                        Act(_gm.TryCraftArmor(item, out err), err);
                    }, 300f));
                }
            }
        }
    }
}
