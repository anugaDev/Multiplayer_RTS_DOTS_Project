using System.Collections.Generic;
using Audio;
using Types;

namespace Units.MovementSystems
{
    public class AttackAudioSourceFactory
    {
        private Dictionary<UnitType, AudioSourceType> _unitToAttackAudioDictionary;

        public AttackAudioSourceFactory()
        {
            _unitToAttackAudioDictionary = new Dictionary<UnitType, AudioSourceType>
            {
                [UnitType.Worker] = AudioSourceType.SwordSwing,
                [UnitType.Warrior] = AudioSourceType.SwordSwing,
                [UnitType.Archer] = AudioSourceType.ArcherShot,
                [UnitType.Ballista] = AudioSourceType.ArcherShot
            };
        }

        public AudioSourceType Get(UnitType unitType)
        {
            return _unitToAttackAudioDictionary[unitType];
        }
    }
}