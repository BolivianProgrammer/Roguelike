using TMPro;
using UnityEngine;

namespace Roguelike.UI
{
    /// <summary>
    /// A physical sign in the world (a painted board on a stall, a rack or a post) instead of text floating in the air.
    /// The text is printed on each face that has a label, each one facing outward, so it never reads mirrored.
    /// Built by HubBuilder; game code only changes <see cref="Text"/>.
    /// </summary>
    public class Signboard : MonoBehaviour
    {
        [SerializeField] TMP_Text front;
        [Tooltip("Optional: the same text on the back face, for boards seen from both sides.")]
        [SerializeField] TMP_Text back;

        public string Text
        {
            get { return front != null ? front.text : ""; }
            set
            {
                if (front != null) front.text = value;
                if (back != null) back.text = value;
            }
        }

        public void Bind(TMP_Text frontLabel, TMP_Text backLabel)
        {
            front = frontLabel;
            back = backLabel;
        }
    }
}
