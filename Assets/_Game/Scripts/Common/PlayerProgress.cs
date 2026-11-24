using System;
using UnityEngine;

namespace Game.Common
{
    // 씬이 바뀌어도 이어지는 플레이어의 레벨, 경험치, 크레딧 (거점과 연습장이 같이 씀)
    // 저장 기능이 없어서 게임을 껐다 켜면 처음 값(PlayerStartData)으로 돌아감
    public static class PlayerProgress
    {
        // 이 레벨을 넘으면 30(+1)처럼 보여줌
        private const int LevelCap = 30;
        private const int HighLevelStart = 20;
        private const int BaseExperience = 100;
        private const int ExperiencePerLevel = 40;

        // 20레벨부터의 경험치통 (마지막 값은 29레벨, 그 뒤로도 같은 값)
        private static readonly int[] _highLevelExperience = { 1100, 1250, 1400, 1550, 1650, 1800, 1950, 2100, 2250, 2400 };

        private static bool _isPrepared;

        public static int PP_Level { get; private set; } = 1;
        public static int PP_Experience { get; private set; }
        public static int PP_Credit { get; private set; }

        /// <summary>레벨, 경험치, 크레딧이 바뀌면 알림</summary>
        public static event Action ProgressChanged;

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProgress()
        {
            _isPrepared = false;
            PP_Level = 1;
            PP_Experience = 0;
            PP_Credit = 0;
            ProgressChanged = null;
        }

        /// <summary>게임을 켠 뒤 처음 불렸을 때만 처음 값을 넣음</summary>
        public static void Prepare(PlayerStartData startData)
        {
            if (_isPrepared || startData == null) return;

            _isPrepared = true;
            PP_Credit = startData.PSD_Credit;
            SetLevel(startData.Level, startData.Experience);
        }

        public static void SetLevel(int level, int experience)
        {
            PP_Level = Mathf.Max(1, level);
            PP_Experience = Mathf.Clamp(experience, 0, GetMaxExperience(PP_Level) - 1);
            ProgressChanged?.Invoke();
        }

        public static void SetCredit(int credit)
        {
            PP_Credit = Mathf.Max(0, credit);
            ProgressChanged?.Invoke();
        }

        /// <summary>이 레벨에서 다음 레벨로 가는 데 필요한 경험치</summary>
        public static int GetMaxExperience(int level)
        {
            if (level < HighLevelStart) return BaseExperience + ExperiencePerLevel * (level - 1);

            int index = Mathf.Min(level - HighLevelStart, _highLevelExperience.Length - 1);
            return _highLevelExperience[index];
        }

        /// <summary>표시할 레벨 글자 (30을 넘으면 30(+3))</summary>
        public static string GetLevelText()
        {
            return PP_Level <= LevelCap ? PP_Level.ToString() : $"{LevelCap}(+{PP_Level - LevelCap})";
        }
    }
}
