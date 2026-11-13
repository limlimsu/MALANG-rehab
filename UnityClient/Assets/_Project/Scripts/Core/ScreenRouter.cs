using System;
using Malang.UI;
using UnityEngine;

namespace Malang.Core
{
    public enum ScreenId
    {
        Boot,
        Calibration,
        Main,
        Stage1,
        Stage2,
        Stage3,
        Result,
        DeviceSelect
    }

    /// <summary>씬 하나 안에서 화면 패널을 하나만 켜 두고 전환한다.</summary>
    public class ScreenRouter : MonoBehaviour
    {
        [SerializeField] private ScreenBase[] screens = new ScreenBase[0];
        [SerializeField] private ScreenId initialScreen = ScreenId.Boot;

        private ScreenBase active;

        public ScreenId Current { get; private set; }
        public ScreenBase Active => active;

        public event Action<ScreenId> Changed;

        private void Awake()
        {
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i] == null)
                {
                    Debug.LogError("ScreenRouter: 비어 있는 화면 참조가 있습니다. Build Scene 을 다시 실행하세요.");
                    continue;
                }

                screens[i].Bind(this);
                screens[i].gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            Show(initialScreen);
        }

        public void Show(ScreenId id)
        {
            ScreenBase target = Find(id);
            if (target == null)
            {
                Debug.LogError("ScreenRouter: 화면이 없습니다: " + id);
                return;
            }

            if (active != null)
            {
                active.OnExit();
                active.gameObject.SetActive(false);
            }

            active = target;
            Current = id;
            active.gameObject.SetActive(true);
            active.OnEnter();
            Changed?.Invoke(id);
        }

        private ScreenBase Find(ScreenId id)
        {
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i] != null && screens[i].Id == id)
                    return screens[i];
            }

            return null;
        }
    }
}
