using Malang.Core;
using Malang.Input;
using UnityEngine;

namespace Malang.UI
{
    /// <summary>
    /// 캐릭터 "말랑이". 실시간 힘에 따라 눌린다 (SPEC §8.2):
    /// scaleX = 1 + g·characterSquishX, scaleY = 1 - g·characterSquishY (기준점: 몸 아래쪽)
    /// 표정(잠든 눈, 새싹)은 M4 에서 추가.
    /// </summary>
    public class CharacterView : MonoBehaviour
    {
        private const float MinChange = 0.01f;

        [SerializeField] private RectTransform squish;
        [SerializeField] private bool followGrip = true;

        private float lastGrip = -1f;

        private void OnDisable()
        {
            Apply(0f);
        }

        private void Update()
        {
            if (!followGrip)
                return;

            IGripInput input = GripSource.Input;
            Apply(input != null ? input.Grip : 0f);
        }

        public void Apply(float g)
        {
            if (squish == null || Mathf.Abs(g - lastGrip) < MinChange)
                return;

            lastGrip = g;
            GameConfig config = GameConfig.Get();
            squish.localScale = new Vector3(1f + g * config.characterSquishX, 1f - g * config.characterSquishY, 1f);
        }
    }
}
