using System;
using System.Text;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Reads resource information (ember, gold, pyre health, hand size) via reflection.
    /// </summary>
    public class ResourceReader
    {
        private readonly BattleManagerCache _cache;
        private readonly HandReader _handReader;

        public ResourceReader(BattleManagerCache cache, HandReader handReader)
        {
            _cache = cache;
            _handReader = handReader;
        }

        /// <summary>
        /// Announce current resources
        /// </summary>
        public void AnnounceResources()
        {
            try
            {
                var sb = new StringBuilder();

                int energy = GetCurrentEnergy();
                if (energy >= 0)
                {
                    sb.Append($"Ember: {energy}. ");
                }

                int gold = GetGold();
                if (gold >= 0)
                {
                    sb.Append($"Gold: {gold}. ");
                }

                int pyreHP = GetPyreHealth();
                int maxPyreHP = GetMaxPyreHealth();
                if (pyreHP >= 0)
                {
                    sb.Append($"Pyre: {pyreHP} of {maxPyreHP}. ");
                }

                int pyreAttack = GetPyreAttack();
                int pyreNumAttacks = GetPyreNumAttacks();
                if (pyreAttack >= 0)
                {
                    if (pyreNumAttacks > 1)
                        sb.Append($"Pyre attack: {pyreAttack} times {pyreNumAttacks}. ");
                    else
                        sb.Append($"Pyre attack: {pyreAttack}. ");
                }

                var hand = _handReader.GetHandCards();
                if (hand != null)
                {
                    sb.Append($"Cards in hand: {hand.Count}.");
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing resources: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read resources", false);
            }
        }

        public int GetCurrentEnergy()
        {
            if (_cache.PlayerManager == null || _cache.GetEnergyMethod == null)
            {
                _cache.FindManagers();
            }

            try
            {
                var result = _cache.GetEnergyMethod?.Invoke(_cache.PlayerManager, null);
                if (result is int energy) return energy;
            }
            catch { }
            return -1;
        }

        public int GetPyreHealth()
        {
            if (_cache.SaveManager == null || _cache.GetTowerHPMethod == null)
            {
                _cache.FindManagers();
            }

            try
            {
                var result = _cache.GetTowerHPMethod?.Invoke(_cache.SaveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        public int GetMaxPyreHealth()
        {
            try
            {
                var result = _cache.GetMaxTowerHPMethod?.Invoke(_cache.SaveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        public int GetPyreAttack()
        {
            if (_cache.SaveManager == null) _cache.FindManagers();
            try
            {
                var method = _cache.SaveManager?.GetType().GetMethod("GetDisplayedPyreAttack", Type.EmptyTypes);
                var result = method?.Invoke(_cache.SaveManager, null);
                if (result is int atk) return atk;
            }
            catch { }
            return -1;
        }

        public int GetPyreNumAttacks()
        {
            if (_cache.SaveManager == null) _cache.FindManagers();
            try
            {
                var method = _cache.SaveManager?.GetType().GetMethod("GetDisplayedPyreNumAttacks", Type.EmptyTypes);
                var result = method?.Invoke(_cache.SaveManager, null);
                if (result is int n) return n;
            }
            catch { }
            return -1;
        }

        public int GetGold()
        {
            if (_cache.SaveManager == null || _cache.GetGoldMethod == null)
            {
                _cache.FindManagers();
            }

            try
            {
                var result = _cache.GetGoldMethod?.Invoke(_cache.SaveManager, null);
                if (result is int gold) return gold;
            }
            catch { }
            return -1;
        }
    }
}
