using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Roguelike.Meta
{
    /// <summary>
    /// Moves between scenes (menu, hub, mission) with a fade to black, so the cut is comfortable in a headset.
    /// </summary>
    public class SceneFlow : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";
        public const string HubScene = "Hub";
        public const string MissionScene = "Mission_Forest";

        const float FadeTime = 0.7f;

        static SceneFlow runner;

        Image overlay;
        bool busy;

        /// <summary>Fades out, loads the scene, fades back in. Ignored while a transition is already running.</summary>
        public static void Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[SceneFlow] Scene '" + sceneName + "' is not in the build settings; reloading the current scene instead.");
                sceneName = SceneManager.GetActiveScene().name;
            }

            Combat.CombatLog.Log("SCENE load requested: " + sceneName);
            if (runner == null)
            {
                var go = new GameObject("Scene Flow");
                DontDestroyOnLoad(go);
                runner = go.AddComponent<SceneFlow>();
            }
            if (!runner.busy) runner.StartCoroutine(runner.Transition(sceneName));
        }

        IEnumerator Transition(string sceneName)
        {
            busy = true;
            yield return Fade(0f, 1f);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (load != null && !load.isDone) yield return null;
            // Nothing that held the old scene still should keep the new one frozen or paused.
            Combat.WorldFreeze.Clear();
            Time.timeScale = 1f;
            yield return null;

            yield return Fade(1f, 0f);
            if (overlay != null) Destroy(overlay.canvas.gameObject);
            busy = false;
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
            {
                SetOverlay(Mathf.Lerp(from, to, t / FadeTime));
                yield return null;
            }
            SetOverlay(to);
        }

        /// <summary>A black sheet just in front of the camera. Rebuilt whenever the camera changes with the scene.</summary>
        void SetOverlay(float alpha)
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (overlay == null || overlay.canvas.transform.parent != cam.transform)
            {
                if (overlay != null) Destroy(overlay.canvas.gameObject);

                var canvasGo = new GameObject("Scene Fade", typeof(Canvas));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 1000;
                var rect = (RectTransform)canvasGo.transform;
                rect.SetParent(cam.transform, false);
                rect.sizeDelta = new Vector2(4000f, 4000f);
                rect.localScale = Vector3.one * 0.001f;
                rect.localPosition = new Vector3(0f, 0f, 0.35f);

                var imageGo = new GameObject("Black", typeof(Image));
                var imageRect = (RectTransform)imageGo.transform;
                imageRect.SetParent(rect, false);
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = Vector2.zero;
                imageRect.offsetMax = Vector2.zero;
                overlay = imageGo.GetComponent<Image>();
                overlay.raycastTarget = false;
            }

            overlay.color = new Color(0f, 0f, 0f, alpha);
        }
    }
}
