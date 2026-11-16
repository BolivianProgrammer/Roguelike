using TMPro;
using UnityEngine;

namespace Roguelike.UI
{
    /// <summary>
    /// The look of every in-world menu and panel: colours, text sizes and the prefabs they are built from.
    /// One asset at Resources/UiTheme; change it in the Inspector and every menu follows.
    /// Made by Roguelike > Build UI Prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject menuPanel;
        public GameObject menuRow;

        [Header("Panel")]
        public Color panel = new Color32(0x12, 0x10, 0x0E, 0xFF);
        public Color border = new Color32(0xD4, 0xA8, 0x2A, 0xFF);
        public Color title = new Color32(0xFF, 0xE9, 0xA8, 0xFF);
        public Color header = new Color32(0xDD, 0xD2, 0xBA, 0xFF);

        [Header("Rows")]
        public Color row = new Color32(0x2E, 0x34, 0x40, 0xFF);
        public Color rowText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public Color rowDetail = new Color32(0xFF, 0xD2, 0x5A, 0xFF);
        public Color hover = new Color32(0xF2, 0xC2, 0x3A, 0xFF);
        public Color hoverText = new Color32(0x10, 0x0C, 0x06, 0xFF);
        public Color disabled = new Color32(0x22, 0x20, 0x1E, 0xFF);
        public Color disabledText = new Color32(0x9C, 0x95, 0x8A, 0xFF);
        [Tooltip("The detail (price) of a line you cannot pick, e.g. too expensive.")]
        public Color disabledDetail = new Color32(0xE0, 0x6A, 0x5A, 0xFF);

        [Header("Placement")]
        [Tooltip("World size of one UI pixel. 0.001 = a 900 px wide panel is 90 cm wide.")]
        public float pixelSize = 0.001f;

        [Header("Text styles (built by Roguelike > Build UI Prefabs)")]
        [Tooltip("Body typeface: descriptions, prompts, HUD lines. Empty = TextMesh Pro's default.")]
        public TMP_FontAsset font;
        [Tooltip("Display typeface (Cinzel): titles, banners, hit markers, card names. Empty = the body typeface.")]
        public TMP_FontAsset displayFont;
        [Tooltip("Titles on panels and cards: display typeface with a thin outline and small shadow.")]
        public Material textTitle;
        [Tooltip("Labels, prompts and HUD: a thin dark outline and a small shadow, readable over any background.")]
        public Material textSoft;
        [Tooltip("Hit markers and damage numbers: a heavy outline and a drop shadow.")]
        public Material textBold;
        [Tooltip("Announcement titles: an outline and a soft dark halo.")]
        public Material textBanner;
        [Tooltip("White, soft-edged band (fades out left and right), tinted per use.")]
        public Sprite fadeBand;
        [Tooltip("White hairline that fades out at both ends, tinted per use.")]
        public Sprite fadeLine;
        [Tooltip("White rounded panel, 9-sliced.")]
        public Sprite panelFill;
        [Tooltip("White rounded frame (border only), 9-sliced, drawn over panelFill.")]
        public Sprite panelFrame;

        [Header("Hit markers")]
        public Color slash = new Color32(0xFF, 0x9A, 0x3C, 0xFF);
        public Color stab = new Color32(0x6F, 0xE3, 0xFF, 0xFF);
        public Color blunt = new Color32(0xE8, 0xE0, 0xD0, 0xFF);
        [Tooltip("Damage at or above this reads as a heavy hit: bigger, hotter number.")]
        public float heavyHit = 40f;
        public Color heavy = new Color32(0xFF, 0x4A, 0x2E, 0xFF);

        [Header("Rarity (blessing choice frames and tags)")]
        public Color rarityCommon = new Color32(0xE8, 0xE0, 0xD0, 0xFF);
        public Color rarityRare = new Color32(0x4F, 0xA3, 0xFF, 0xFF);
        public Color rarityLegendary = new Color32(0xFF, 0x9A, 0x2E, 0xFF);
        [Tooltip("Colour that numbers in descriptions are picked out in.")]
        public Color highlight = new Color32(0xFF, 0xD2, 0x5A, 0xFF);

        public Color RarityColor(Meta.Rarity rarity) =>
            rarity == Meta.Rarity.Legendary ? rarityLegendary : rarity == Meta.Rarity.Rare ? rarityRare : rarityCommon;

        [Header("Banner and HUD")]
        public Color band = new Color32(0x08, 0x07, 0x06, 0xD0);
        public Color hudPanel = new Color32(0x0E, 0x0C, 0x0A, 0xC8);
        public Color health = new Color32(0xC8, 0x2A, 0x1E, 0xFF);
        public Color stamina = new Color32(0xE8, 0xB8, 0x3A, 0xFF);
        public Color barBack = new Color32(0x1C, 0x18, 0x14, 0xF0);

        static UiTheme instance;

        public static UiTheme Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<UiTheme>("UiTheme");
                if (instance == null) instance = CreateInstance<UiTheme>();
                return instance;
            }
        }
    }
}
