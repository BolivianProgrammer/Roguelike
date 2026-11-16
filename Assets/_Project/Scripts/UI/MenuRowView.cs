using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.UI
{
    /// <summary>One selectable line of a menu: a background, a label on the left and a detail (a price) on the right.</summary>
    public class MenuRowView : MonoBehaviour
    {
        public RectTransform rect;
        public Image background;
        public TMP_Text label;
        public TMP_Text detail;

        /// <summary>Colours the row for its state, from the theme.</summary>
        public void Paint(UiTheme theme, bool enabled, bool hovered)
        {
            background.color = !enabled ? theme.disabled : hovered ? theme.hover : theme.row;
            label.color = !enabled ? theme.disabledText : hovered ? theme.hoverText : theme.rowText;
            detail.color = !enabled ? theme.disabledDetail : hovered ? theme.hoverText : theme.rowDetail;
            // The highlighted line also turns bold, so it stands out even for colour-blind players.
            label.fontStyle = hovered ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            detail.fontStyle = hovered ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
        }
    }
}
