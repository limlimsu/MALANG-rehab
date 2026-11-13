using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 연기 구름 효과 (UI 이미지 풀, 파티클 시스템 없이).
    /// Emit 한 구름은 움직이면서 커지고 흐려지다 사라진다. 매 프레임 할당 없음.
    /// 위치·속도 단위는 이 오브젝트 부모 기준 anchoredPosition(px).
    /// </summary>
    public class SmokePuffs : MonoBehaviour
    {
        private const float GrowFactor = 1.6f;
        private const float StartAlpha = 0.85f;
        private const float Drag = 1.8f;

        [SerializeField] private Image[] puffs = new Image[0];

        private RectTransform[] rects;
        private float[] age;
        private float[] life;
        private float[] startSize;
        private Vector2[] velocity;
        private bool[] alive;
        private int next;

        private void Awake()
        {
            int n = puffs.Length;
            rects = new RectTransform[n];
            age = new float[n];
            life = new float[n];
            startSize = new float[n];
            velocity = new Vector2[n];
            alive = new bool[n];

            for (int i = 0; i < n; i++)
                rects[i] = puffs[i].rectTransform;

            Clear();
        }

        public void Emit(Vector2 position, Vector2 initialVelocity, float size, float lifetime)
        {
            if (puffs.Length == 0)
                return;

            int i = next;
            next = (next + 1) % puffs.Length; // 다 쓰면 가장 오래된 것부터 다시 쓴다

            alive[i] = true;
            age[i] = 0f;
            life[i] = Mathf.Max(0.05f, lifetime);
            startSize[i] = size;
            velocity[i] = initialVelocity;
            rects[i].anchoredPosition = position;
            rects[i].sizeDelta = new Vector2(size, size);
            puffs[i].gameObject.SetActive(true);
            SetAlpha(i, StartAlpha);
        }

        public void Clear()
        {
            if (alive == null)
                return;

            for (int i = 0; i < puffs.Length; i++)
            {
                alive[i] = false;
                puffs[i].gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (alive != null)
                Clear();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < puffs.Length; i++)
            {
                if (!alive[i])
                    continue;

                age[i] += dt;
                float k = age[i] / life[i];
                if (k >= 1f)
                {
                    alive[i] = false;
                    puffs[i].gameObject.SetActive(false);
                    continue;
                }

                velocity[i] *= Mathf.Max(0f, 1f - Drag * dt);
                rects[i].anchoredPosition += velocity[i] * dt;
                float s = startSize[i] * (1f + GrowFactor * k);
                rects[i].sizeDelta = new Vector2(s, s);
                SetAlpha(i, StartAlpha * (1f - k));
            }
        }

        private void SetAlpha(int i, float a)
        {
            Color c = puffs[i].color;
            c.a = a;
            puffs[i].color = c;
        }
    }
}
