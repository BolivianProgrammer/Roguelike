using UnityEngine;
using UnityEngine.XR;

namespace Roguelike.Player
{
    /// <summary>
    /// Keeps the physics step in sync with the headset's refresh rate (72/90/120 Hz on Quest 3).
    /// If physics runs slower than the display, physics hands visibly stutter.
    /// </summary>
    public class PhysicsRateSync : MonoBehaviour
    {
        void Update()
        {
            float refreshRate = XRDevice.refreshRate;
            if (refreshRate <= 1f) return;

            float step = 1f / refreshRate;
            if (!Mathf.Approximately(Time.fixedDeltaTime, step))
                Time.fixedDeltaTime = step;
        }
    }
}
