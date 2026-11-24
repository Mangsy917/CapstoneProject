using UnityEngine;

namespace Game.Common
{
    // 게임을 시작했을 때의 레벨, 경험치, 크레딧
    // 거점 씬의 HubInventory 인스펙터에서 조정함 (거점과 연습장이 같은 값을 씀)
    [CreateAssetMenu(fileName = "D_PlayerStart", menuName = "Game/Player Start Data")]
    public class PlayerStartData : ScriptableObject
    {
        [Min(1)]
        [SerializeField] private int _level = 1;
        [Min(0)]
        [SerializeField] private int _experience;
        [Min(0)]
        [SerializeField] private int PSD_credit = 1000;

        public int Level => _level;
        public int Experience => _experience;
        public int PSD_Credit => PSD_credit;
    }
}
