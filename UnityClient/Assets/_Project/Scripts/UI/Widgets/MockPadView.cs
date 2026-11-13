using System;
using Malang.Input;
using UnityEngine;

namespace Malang.UI
{
    /// <summary>
    /// 화면 세기 버튼 4개 (살짝 / 보통 / 세게 / 꾹) — 키보드 없는 태블릿 시연용.
    /// 모의 입력일 때만 보이고, 보호자 모드에서 숨길 수 있다.
    /// buttons[i] 의 i 는 MockGripInput.LevelSoft..LevelMax 와 같다.
    /// </summary>
    public class MockPadView : MonoBehaviour
    {
        private const string PrefEnabled = "malang.mockPad";

        private static int enabledCache = -1;

        [SerializeField] private GameObject buttonsRoot;
        [SerializeField] private HoldButton[] buttons = new HoldButton[0];

        private Action<bool>[] handlers;

        public static bool PadEnabled
        {
            get
            {
                if (enabledCache < 0)
                    enabledCache = PlayerPrefs.GetInt(PrefEnabled, 1);
                return enabledCache == 1;
            }
            set
            {
                enabledCache = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefEnabled, enabledCache);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabledCache = -1;
        }

        private void Awake()
        {
            handlers = new Action<bool>[buttons.Length];

            for (int i = 0; i < buttons.Length; i++)
            {
                int level = i;
                handlers[i] = held =>
                {
                    GripSource source = GripSource.Instance;
                    MockGripInput mock = source != null ? source.Mock : null;
                    if (mock != null)
                        mock.SetButtonHeld(level, held);
                };
                buttons[i].HeldChanged += handlers[i];
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                    buttons[i].HeldChanged -= handlers[i];
            }
        }

        private void Update()
        {
            GripSource source = GripSource.Instance;
            bool show = PadEnabled && source != null && source.Kind == DeviceKind.Mock;

            if (buttonsRoot.activeSelf != show)
                buttonsRoot.SetActive(show);
        }
    }
}
