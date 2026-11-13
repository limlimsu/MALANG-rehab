using Malang.Core;
using TMPro;
using UnityEngine;

namespace Malang.UI
{
    public enum FontRole
    {
        Title,
        Body
    }

    /// <summary>
    /// TMP 텍스트를 Loc 키로 채우고, 언어에 맞는 폰트(제목/본문)로 바꾼다.
    /// key 가 비어 있으면 폰트만 바꾼다(숫자 표시 등).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private FontRole role = FontRole.Body;

        private TMP_Text label;
        private object[] args;

        public TMP_Text Label
        {
            get
            {
                if (label == null)
                    label = GetComponent<TMP_Text>();
                return label;
            }
        }

        public string Key
        {
            get { return key; }
            set
            {
                if (key == value)
                    return;
                key = value;
                Refresh();
            }
        }

        /// <summary>에디터 빌더용 초기 설정.</summary>
        public void Setup(string newKey, FontRole newRole)
        {
            key = newKey;
            role = newRole;
            Refresh();
        }

        /// <summary>키와 형식 인자를 함께 바꾼다. 인자가 null 이면 인자 없음. 값이 바뀔 때만 호출할 것.</summary>
        public void Set(string newKey, object arg)
        {
            key = newKey;
            args = arg == null ? null : new[] { arg };
            Refresh();
        }

        /// <summary>형식 인자를 바꾼다. 값이 바뀔 때만 호출할 것(할당 발생).</summary>
        public void SetArgs(params object[] newArgs)
        {
            args = newArgs;
            Refresh();
        }

        /// <summary>Loc 키 없이 직접 문자열을 넣는다(숫자 등). 폰트는 계속 언어를 따른다.</summary>
        public void SetRaw(string text)
        {
            key = string.Empty;
            args = null;
            ApplyFont();
            Label.text = text;
        }

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
        }

        public void Refresh()
        {
            ApplyFont();

            if (string.IsNullOrEmpty(key))
                return;

            Label.text = args == null || args.Length == 0 ? Loc.T(key) : Loc.F(key, args);
        }

        private void ApplyFont()
        {
            TMP_FontAsset font = FontFor(role);
            if (font != null && Label.font != font)
                Label.font = font;
        }

        public static TMP_FontAsset FontFor(FontRole role)
        {
            GameConfig config = GameConfig.Get();

            if (Loc.IsEnglish)
                return role == FontRole.Title ? config.enTitleFont : config.enBodyFont;

            return role == FontRole.Title ? config.koTitleFont : config.koBodyFont;
        }
    }
}
