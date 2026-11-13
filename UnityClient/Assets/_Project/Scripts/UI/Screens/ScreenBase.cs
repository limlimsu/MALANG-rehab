using Malang.Core;
using UnityEngine;

namespace Malang.UI
{
    /// <summary>ScreenRouter 가 켜고 끄는 화면 패널의 기반 클래스.</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        public abstract ScreenId Id { get; }

        protected ScreenRouter Router { get; private set; }
        protected GameConfig Config => GameConfig.Get();

        public void Bind(ScreenRouter router)
        {
            Router = router;
        }

        /// <summary>화면이 켜진 직후 호출.</summary>
        public virtual void OnEnter()
        {
        }

        /// <summary>화면이 꺼지기 직전 호출.</summary>
        public virtual void OnExit()
        {
        }

        protected void Go(ScreenId id)
        {
            if (Router != null)
                Router.Show(id);
        }
    }
}
