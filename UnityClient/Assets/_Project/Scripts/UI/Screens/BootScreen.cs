using System.Collections;
using Malang.Core;
using Malang.Input;
using UnityEngine;

namespace Malang.UI
{
    /// <summary>
    /// 로고 + "말랑이를 연결하는 중…".
    /// bootDelaySeconds 후: 저장된 기기가 있으면 자동 연결 → 메인, 없으면 → 기기 선택.
    /// M5: 센서 입력이고 보정이 필요하면 보정 화면으로 간다.
    /// </summary>
    public class BootScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Boot;

        private Coroutine routine;

        public override void OnEnter()
        {
            routine = StartCoroutine(Proceed());
        }

        public override void OnExit()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
        }

        private IEnumerator Proceed()
        {
            yield return new WaitForSecondsRealtime(Config.bootDelaySeconds);
            routine = null;

            GripSource source = GripSource.Instance;
            if (source != null && source.ConnectSaved())
                Go(ScreenId.Main);
            else
                Go(ScreenId.DeviceSelect);
        }
    }
}
