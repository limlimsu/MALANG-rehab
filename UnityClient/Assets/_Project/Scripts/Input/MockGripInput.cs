using Malang.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Malang.Input
{
    /// <summary>
    /// 센서 없이 힘을 흉내 내는 모의 입력 (Input System API 사용).
    ///
    /// 누르고 있는 동안 목표 세기까지 곡선으로 오르고, 떼면 0 으로 내려간다.
    ///   키 1 = 살짝(3단계 살짝 구간 가운데), 2 = 보통, 3 = 세게, Space = 100%
    ///   화면 세기 버튼 4개(SetButtonHeld), 마우스 왼쪽 버튼(화면 빈 곳에서 누르기 시작) = 100%
    ///   ↑ / ↓ : 누르고 있는 동안 목표값 ±mockFineStep
    /// 오르기:  p += (goal - p)(1 - e^(-k_up·dt))
    /// 내리기:  p -= (p - goal)(1 - e^(-k_down·dt)), 다 떼고 p &lt; zeroSnap 이면 0
    /// 디버그 슬라이더로 값을 직접 지정하는 동안(override)에는 위 식을 멈춘다.
    /// </summary>
    public class MockGripInput : IGripDevice
    {
        public const int LevelSoft = 0;
        public const int LevelNormal = 1;
        public const int LevelStrong = 2;
        public const int LevelMax = 3;
        public const int LevelCount = 4;

        private static readonly int[] NoChannels = new int[0];
        private const float FullForce = 100f;

        private readonly bool[] buttonHeld = new bool[LevelCount];
        private GameConfig config;
        private float value;
        private float fineOffset;
        private bool mouseGripping;
        private bool overriding;
        private float overrideValue;
        private int sampleCount;

        public DeviceKind Kind => DeviceKind.Mock;
        public bool IsConnected => true;
        public bool IsNormalized => true;
        public int ChannelCount => 0;
        public int[] Channels => NoChannels;
        public float Sum => value;
        public long DeviceMs => 0;
        public int SampleCount => sampleCount;
        public float SampleRateHz => 0f;

        /// <summary>현재 목표 세기(디버그 표시용). 아무것도 안 누르면 0.</summary>
        public float CurrentGoal { get; private set; }

        public bool IsOverriding => overriding;

        public void Open(GameConfig cfg)
        {
            config = cfg;
            value = 0f;
            fineOffset = 0f;
        }

        public void Close()
        {
            for (int i = 0; i < buttonHeld.Length; i++)
                buttonHeld[i] = false;
            overriding = false;
        }

        public void SetButtonHeld(int level, bool held)
        {
            if (level >= 0 && level < LevelCount)
                buttonHeld[level] = held;
        }

        public void SetOverride(float v)
        {
            overriding = true;
            overrideValue = Mathf.Clamp(v, 0f, FullForce);
        }

        public void EndOverride()
        {
            overriding = false;
        }

        /// <summary>세기 단계의 목표값. 살짝·보통·세게는 3단계 등급 구간의 가운데.</summary>
        public float LevelTarget(int level)
        {
            switch (level)
            {
                case LevelSoft: return (config.s3SoftMin + config.s3SoftMax) * 0.5f;
                case LevelNormal: return (config.s3NormalMin + config.s3NormalMax) * 0.5f;
                case LevelStrong: return (config.s3StrongMin + config.s3StrongMax) * 0.5f;
                default: return FullForce;
            }
        }

        public void Tick(float dt, double now)
        {
            sampleCount++;

            if (overriding)
            {
                value = overrideValue;
                CurrentGoal = overrideValue;
                return;
            }

            float target = -1f;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed)
                    target = Mathf.Max(target, LevelTarget(LevelSoft));
                if (keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed)
                    target = Mathf.Max(target, LevelTarget(LevelNormal));
                if (keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed)
                    target = Mathf.Max(target, LevelTarget(LevelStrong));
                if (keyboard.spaceKey.isPressed)
                    target = Mathf.Max(target, LevelTarget(LevelMax));
            }

            for (int i = 0; i < LevelCount; i++)
            {
                if (buttonHeld[i])
                    target = Mathf.Max(target, LevelTarget(i));
            }

            if (UpdateMouseGrip())
                target = Mathf.Max(target, LevelTarget(LevelMax));

            bool holding = target >= 0f;

            if (holding && keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame)
                    fineOffset += config.mockFineStep;
                if (keyboard.downArrowKey.wasPressedThisFrame)
                    fineOffset -= config.mockFineStep;
            }
            else if (!holding)
            {
                fineOffset = 0f;
            }

            float goal = holding ? Mathf.Clamp(target + fineOffset, 0f, FullForce) : 0f;
            CurrentGoal = goal;

            if (goal > value)
                value += (goal - value) * (1f - Mathf.Exp(-config.mockRiseRate * dt));
            else
                value -= (value - goal) * (1f - Mathf.Exp(-config.mockFallRate * dt));

            if (!holding && value < config.mockZeroSnap)
                value = 0f;
        }

        /// <summary>화면 빈 곳(UI 위가 아닌 곳)에서 누르기 시작한 마우스 왼쪽 버튼.</summary>
        private bool UpdateMouseGrip()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed)
            {
                mouseGripping = false;
                return false;
            }

            if (!mouseGripping && mouse.leftButton.wasPressedThisFrame)
            {
                EventSystem eventSystem = EventSystem.current;
                mouseGripping = eventSystem == null || !eventSystem.IsPointerOverGameObject();
            }

            return mouseGripping;
        }
    }
}
