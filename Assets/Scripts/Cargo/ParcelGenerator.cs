using System;
using System.Collections.Generic;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Bir gunun kargolarini uretir.
    ///
    /// DETERMINISTIK: UnityEngine.Random degil System.Random kullanir ve
    /// disaridan tohum alir. Ayni tohum -> ayni kargolar. Bu sayede
    /// "gun 3'te hangi kargolar vardi" sorusu save dosyasindan yeniden
    /// kurulabilir ve testler kararli olur.
    ///
    /// Bu sinif MonoBehaviour DEGIL: mantik testleri sahne acmadan calisir.
    /// </summary>
    public sealed class ParcelGenerator
    {
        // Turkce isim havuzu. Cogu Turkce oyunda gercekci hissettirir ve
        // kargoyu soyut bir sayidan insan yapmis kilar.
        private static readonly string[] RecipientNames =
        {
            "Ahmet Yilmaz", "Ayse Kaya", "Mehmet Demir", "Fatma Sahin",
            "Ali Vural", "Zeynep Aydin", "Mustafa Ozturk", "Elif Korkmaz",
            "Ibrahim Celik", "Meryem Dogan", "Hasan Aslan", "Hatice Yildiz",
            "Emre Aksoy", "Sultan Kurt", "Burak Polat", "Merve Tekin",
            "Onur Can", "Esra Bozkurt", "Kerem Sarikaya", "Derya Akin",
        };

        /// <summary>Oncelik dagilimi. Acil kargo nadir olmali, yoksa oyun zorlasir.</summary>
        private const float UrgentChance = 0.12f;
        private const float ExpressChance = 0.30f;   // Urgent'a gore kosullu

        private readonly ParcelType[] _types;
        private readonly int _destinationCount;
        private readonly float _baseDeadlineSeconds;
        private Random _random;
        private int _nextId = 1;

        /// <param name="types">Kullanilacak kargo turleri (bos olamaz).</param>
        /// <param name="destinationCount">Hedef adres sayisi. 0 ise uretim yapilmaz.</param>
        /// <param name="baseDeadlineSeconds">Normal oncelik icin temel teslim suresi.</param>
        public ParcelGenerator(ParcelType[] types, int destinationCount, float baseDeadlineSeconds)
        {
            if (types == null || types.Length == 0)
                throw new ArgumentException("En az bir ParcelType gerekli.", nameof(types));
            if (destinationCount < 1)
                throw new ArgumentException("En az bir hedef adres gerekli.", nameof(destinationCount));

            _types = (ParcelType[])types.Clone();
            _destinationCount = destinationCount;
            _baseDeadlineSeconds = Math.Max(30f, baseDeadlineSeconds);
        }

        /// <summary>Yeni gun: tohumu degistirir ve sayaci sifirlar.</summary>
        public void ResetForNewDay(int seed)
        {
            _random = new Random(seed);
            _nextId = 1;
        }

        /// <summary>
        /// <paramref name="count"/> adet kargo uretir. Uretilen her kargo
        /// Once "depo" listesine gider; hicbiri otomatik yuklenmez.
        /// </summary>
        public List<Parcel> Generate(int count)
        {
            if (_random == null) ResetForNewDay(0);
            if (count < 0) count = 0;

            var result = new List<Parcel>(count);
            for (int i = 0; i < count; i++)
                result.Add(GenerateOne());

            return result;
        }

        private Parcel GenerateOne()
        {
            ParcelType type = _types[_random.Next(_types.Length)];
            if (type == null) type = _types[0];

            float weight = Range(type.minWeightKg, type.maxWeightKg);
            float volume = Range(type.minVolumeLiters, type.maxVolumeLiters);
            ParcelPriority priority = RollPriority();

            float deadline = _baseDeadlineSeconds * priority.TimeMultiplier();
            int destination = _random.Next(_destinationCount);
            string recipient = RecipientNames[_random.Next(RecipientNames.Length)];

            // Tabani yukseltmek, hafif kargolari da yeterince degerli kilar.
            int baseValue = (int)Math.Round(
                type.paymentPerKg * weight * priority.PaymentMultiplier(),
                MidpointRounding.AwayFromZero);

            return new Parcel(_nextId++, type, recipient, destination,
                              weight, volume, priority, deadline, baseValue);
        }

        private ParcelPriority RollPriority()
        {
            float roll = NextFloat();

            if (roll < UrgentChance) return ParcelPriority.Urgent;
            if (roll < UrgentChance + ExpressChance) return ParcelPriority.Express;
            return ParcelPriority.Normal;
        }

        private float Range(float min, float max)
        {
            if (max < min) max = min;
            return min + NextFloat() * (max - min);
        }

        private float NextFloat() => (float)_random.NextDouble();
    }
}
