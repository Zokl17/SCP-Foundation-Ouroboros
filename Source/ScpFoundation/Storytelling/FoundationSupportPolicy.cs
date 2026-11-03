using System;

namespace ScpFoundation.Storytelling
{
    public sealed class FoundationSupportProgress
    {
        public const int MaximumTrust = 100;
        public const int InitialTrust = 10;
        public const int StableTicksPerTrust = 30000;
        public const int SupplyTrustCost = 10;
        public const int SupplyCooldownTicks = 900000;
        public const int DossierTrustCost = 20;
        public const int SecurityTrustCost = 30;
        public const int SecurityCooldownTicks = 900000;
        public const int Epsilon11TrustCost = 50;
        public const int Epsilon11CooldownTicks = 1200000;

        public int trust;
        public int stableObjectTicks;
        public int lastTick = -1;
        public int nextSupplyTick = -1;
        public int supplyRequests;
        public bool unlocked;
        public bool dossierRequested;
        public int nextSecurityTick = -1;
        public int securityRequests;
        public int nextEpsilon11Tick = -1;
        public int epsilon11Requests;

        public bool Unlock()
        {
            if (unlocked) return false;
            unlocked = true;
            trust = Math.Max(trust, InitialTrust);
            lastTick = -1;
            return true;
        }

        public int Advance(int now, int stableObjects)
        {
            if (now < 0) return 0;
            long elapsed = lastTick < 0 ? 0L : (long)now - lastTick;
            lastTick = now;
            if (!unlocked || stableObjects <= 0 || elapsed < 0 || elapsed > 250 || trust >= MaximumTrust) return 0;

            long accumulated = (long)stableObjectTicks + elapsed * stableObjects;
            int gained = (int)Math.Min(MaximumTrust - trust, accumulated / StableTicksPerTrust);
            trust += gained;
            stableObjectTicks = trust >= MaximumTrust ? 0 : (int)(accumulated % StableTicksPerTrust);
            return gained;
        }

        public int SupplyCooldownRemaining(int now)
        {
            return nextSupplyTick < 0 ? 0 : (int)Math.Max(0L, (long)nextSupplyTick - now);
        }

        public bool CanRequestSupplies(int now)
        {
            return unlocked && now >= 0 && trust >= SupplyTrustCost && SupplyCooldownRemaining(now) == 0;
        }

        public bool SpendOnSupplies(int now)
        {
            if (!CanRequestSupplies(now)) return false;
            trust -= SupplyTrustCost;
            nextSupplyTick = (int)Math.Min(int.MaxValue, (long)now + SupplyCooldownTicks);
            supplyRequests++;
            return true;
        }

        public bool CanRequestDossier(bool alreadyDiscovered)
        {
            return unlocked && !dossierRequested && !alreadyDiscovered && trust >= DossierTrustCost;
        }

        public bool SpendOnDossier(bool alreadyDiscovered)
        {
            if (!CanRequestDossier(alreadyDiscovered)) return false;
            trust -= DossierTrustCost;
            dossierRequested = true;
            return true;
        }

        public bool TrySpend(int amount)
        {
            if (!unlocked || amount < 0 || trust < amount) return false;
            trust -= amount;
            return true;
        }

        public bool TrySpendOnCertification(int amount)
        {
            if (amount < 0 || trust < amount) return false;
            trust -= amount;
            return true;
        }

        public int SecurityCooldownRemaining(int now)
        {
            return nextSecurityTick < 0 ? 0 : (int)Math.Max(0L, (long)nextSecurityTick - now);
        }

        public bool CanRequestSecurity(int now)
        {
            return unlocked && now >= 0 && trust >= SecurityTrustCost && SecurityCooldownRemaining(now) == 0;
        }

        public bool SpendOnSecurity(int now)
        {
            if (!CanRequestSecurity(now)) return false;
            trust -= SecurityTrustCost;
            nextSecurityTick = (int)Math.Min(int.MaxValue, (long)now + SecurityCooldownTicks);
            securityRequests++;
            return true;
        }

        public int Epsilon11CooldownRemaining(int now)
        {
            return nextEpsilon11Tick < 0 ? 0 : (int)Math.Max(0L, (long)nextEpsilon11Tick - now);
        }

        public bool CanRequestEpsilon11(int now)
        {
            return unlocked && now >= 0 && trust >= Epsilon11TrustCost && Epsilon11CooldownRemaining(now) == 0;
        }

        public bool SpendOnEpsilon11(int now)
        {
            if (!CanRequestEpsilon11(now)) return false;
            trust -= Epsilon11TrustCost;
            nextEpsilon11Tick = (int)Math.Min(int.MaxValue, (long)now + Epsilon11CooldownTicks);
            epsilon11Requests++;
            return true;
        }
    }
}
