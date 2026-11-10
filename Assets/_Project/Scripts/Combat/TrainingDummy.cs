using System.Collections;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// A target for tuning weapon feel: flashes and shows the damage of every hit, and never dies.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer flashRenderer;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashDuration = 0.1f;

        MaterialPropertyBlock propertyBlock;
        Color baseColor = Color.gray;
        Coroutine flashRoutine;

        public float TotalDamage { get; private set; }
        public int HitCount { get; private set; }

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (flashRenderer != null && flashRenderer.sharedMaterial != null && flashRenderer.sharedMaterial.HasProperty(BaseColorId))
                baseColor = flashRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        public void TakeDamage(DamageInfo info)
        {
            TotalDamage += info.Amount;
            HitCount++;
            DamagePopup.Spawn(info.Point, info.Amount, info.Type);
            Debug.Log($"[Dummy] {info.Type} {info.Amount:0} damage at {info.ImpactSpeed:0.0} m/s (total {TotalDamage:0}, hits {HitCount})");

            if (flashRenderer == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }

        IEnumerator Flash()
        {
            SetColor(flashColor);
            yield return new WaitForSeconds(flashDuration);
            SetColor(baseColor);
            flashRoutine = null;
        }

        void SetColor(Color color)
        {
            flashRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            flashRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
