using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Roguelike.Meta
{
    /// <summary>One line of the title screen's menu. Reports mouse hover and clicks to the menu.</summary>
    public class MainMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] TMP_Text label;

        MainMenu menu;
        int index;

        public TMP_Text Label => label;
        /// <summary>False for entries that cannot be picked right now, such as Continue without a save.</summary>
        public bool Usable { get; set; } = true;

        public void Bind(MainMenu owner, int itemIndex)
        {
            menu = owner;
            index = itemIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (menu != null) menu.Select(index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (menu != null) menu.Choose(index);
        }
    }
}
