using UnityEngine;

namespace MuscSwarm
{
    /// <summary>
    /// Wizualizacja toru lotu drona przez TrailRenderer.
    /// Komponent doczepiany w runtime (np. przez Simulation) lub recznie do prefabu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DroneTrail : MonoBehaviour
    {
        TrailRenderer trail;

        /// <summary>
        /// Konfiguruje TrailRenderer (tworzy jesli nie ma). Idempotentne - mozna wywolac wielokrotnie.
        /// </summary>
        /// <param name="color">Kolor sladu.</param>
        /// <param name="duration">Czas zycia segmentu w sekundach. Ignorowane gdy persistent = true.</param>
        /// <param name="width">Szerokosc sladu u zrodla (jednostki Unity).</param>
        /// <param name="persistent">Jesli true: slad nigdy nie znika ani nie blaknie (trwale linie w powietrzu).</param>
        public void Configure(Color color, float duration, float width, bool persistent = false)
        {
            EnsureTrail();

            if (trail.sharedMaterial == null)
                trail.sharedMaterial = GetOrCreateDefaultMaterial();

            // Mathf.Infinity zatrzymuje cykl usuwania starych wertexow.
            trail.time = persistent ? Mathf.Infinity : duration;
            trail.startWidth = width;
            // Persistent: koncowka tej samej szerokosci (no taper), dzieki czemu linia wyglada
            // jak ciagly tor. Fade mode: zwezenie do zera dla efektu komety.
            trail.endWidth = persistent ? width : 0f;
            trail.minVertexDistance = 0.05f;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.autodestruct = false;
            trail.emitting = true;

            Gradient g = new Gradient();
            if (persistent)
            {
                // Pelna nieprzezroczystosc na calej dlugosci - slad nigdy nie blaknie.
                g.SetKeys(
                    new[]
                    {
                        new GradientColorKey(color, 0f),
                        new GradientColorKey(color, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
            }
            else
            {
                // Standardowy efekt komety: alpha blaknie liniowo do zera od ogona.
                g.SetKeys(
                    new[]
                    {
                        new GradientColorKey(color, 0f),
                        new GradientColorKey(color, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(color.a, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
            }
            trail.colorGradient = g;
        }

        /// <summary>Czyszczenie historii sladu (np. po teleportowaniu drona).</summary>
        public void ClearTrail()
        {
            if (trail != null) trail.Clear();
        }

        /// <summary>Wlaczanie / wylaczanie emisji bez usuwania historii.</summary>
        public void SetEmitting(bool on)
        {
            if (trail != null) trail.emitting = on;
        }

        void EnsureTrail()
        {
            if (trail == null) trail = GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
        }

        // ----- Shared material (jeden na caly projekt zeby nie alokowac N kopii) -----

        static Material cachedMaterial;

        static Material GetOrCreateDefaultMaterial()
        {
            if (cachedMaterial != null) return cachedMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            cachedMaterial = new Material(shader) { name = "DroneTrail (runtime)" };
            cachedMaterial.hideFlags = HideFlags.DontSave;
            return cachedMaterial;
        }
    }
}
