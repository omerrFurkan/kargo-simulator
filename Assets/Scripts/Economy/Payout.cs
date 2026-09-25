namespace KargoSim.Economy
{
    /// <summary>
    /// Bir teslimin odeme sonucu. Para ve XP AYRI alanlardir: UI her ikisini
    /// de gosterir ama kaynaklar farklidir.
    /// </summary>
    public readonly struct Payout
    {
        /// <summary>Kargo degerinden hesaplanan ana odeme (TL).</summary>
        public int Payment { get; }

        /// <summary>Erken teslim bahsisi (TL).</summary>
        public int Bonus { get; }

        /// <summary>Basarisizlik cezasi (TL, negatif etki).</summary>
        public int Penalty { get; }

        public int Xp { get; }

        /// <summary>Toplam para: odeme + bahsis - ceza.</summary>
        public int TotalMoney => Payment + Bonus - Penalty;

        public Payout(int payment, int bonus, int penalty, int xp)
        {
            Payment = payment;
            Bonus = bonus;
            Penalty = penalty;
            Xp = xp;
        }

        public static readonly Payout Nothing = new Payout(0, 0, 0, 0);

        public override string ToString() =>
            $"Odeme {Payment} + Bahsis {Bonus} - Ceza {Penalty} = {TotalMoney} TL, {Xp} XP";
    }
}
