using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.UI
{
    /// <summary>The parts of the menu panel prefab that code fills in. Built by Roguelike > Build UI Prefabs.</summary>
    public class MenuPanelView : MonoBehaviour
    {
        public RectTransform root;
        public Image background;
        public Image border;
        public TMP_Text title;
        public TMP_Text header;
        public RectTransform rows;
        public TMP_Text footer;
    }
}
