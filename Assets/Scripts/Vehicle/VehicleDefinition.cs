using UnityEngine;

namespace KargoSim.Vehicle
{
    /// <summary>
    /// Bir aracin fiziksel ozellikleri. Kod icine gomulmus sabitler yerine
    /// Inspector'dan degistirilebilir tutulur: yeni arac eklemek icin
    /// script degil, sadece yeni bir asset yeterlidir.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VehicleDefinition",
        menuName = "KargoSim/Vehicle Definition",
        order = 1)]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [Header("Kutle")]
        [Tooltip("Aracin agirligi (kg). Agir arac daha yavas hizlanir, daha geç durur.")]
        [Min(100f)] public float mass = 1200f;

        [Tooltip("Agirlik merkezi aracin ne kadar asagi inerse araç o kadar cok yatir, " +
                 "takla atma riski azalir. Deger: yerden yukseklik (m).")]
        [Range(0f, 1.5f)] public float centerOfMassHeight = 0.45f;

        [Header("Hiz")]
        [Tooltip("Ileri yondeki azami hiz (m/s). 30 m/s = 108 km/h.")]
        [Min(1f)] public float maxSpeed = 30f;

        [Tooltip("Geri yondeki azami hiz (m/s). Geri yavas olur, cunku yol kazanmak zor.")]
        [Min(1f)] public float maxReverseSpeed = 12f;

        [Header("Motor")]
        [Tooltip("Toplam motor torku (Nm), 4 teker arasinda paylasilir. " +
                 "Gercek bir aracin toplam torku 200-400 Nm bandindadir; " +
                 "oyun icin daha guclu degerler kullanilir ki his " +
                 "abartili ama okunakli olsun.")]
        [Min(100f)] public float engineForce = 3000f;

        [Tooltip("El freni tutarken uygulanan fren kuvveti carpani (1 = normal fren).")]
        [Min(0f)] public float handbrakeStrength = 3f;

        [Header("Fren")]
        [Tooltip("Teker teker uygulanan fren torku (Nm).")]
        [Min(100f)] public float brakeForce = 3500f;

        [Header("Direksiyon")]
        [Tooltip("Dururken azami donus acisi (derece).")]
        [Range(5f, 60f)] public float maxSteerAngle = 32f;

        [Tooltip("Azami hizdaki donus acisinin, dururkenki acinin kac katı " +
                 "olacagi. 0.35 = hizliyken aci %35'e iner.\n" +
                 "0 = arac hizliyken hic donmez (kullanilmaz), " +
                 "1 = donus hic kisalmaz (yandan kayma hissi).")]
        [Range(0.1f, 1f)] public float minSteerFactor = 0.35f;

        [Header("Yapisal Tutunma")]
        [Tooltip("Yan kayma direnci. Buyuk deger = kayma az, kaygan yol hissi yok. " +
                 "0 = buz gibi kayar.")]
        [Range(0f, 40f)] public float lateralGrip = 12f;

        [Tooltip("Hiz karesiyle artan yapisan kuvvet. Arac hizlandikca yere " +
                 "basar, zirve yapmaz. Deger 0 ise devrilme riski artar.")]
        [Range(0f, 200f)] public float downforce = 60f;

        [Tooltip("Arac dururken devrilmisse kendini kaldirma hizi (derece/sn).")]
        [Range(10f, 360f)] public float autoUprightSpeed = 120f;

        [Header("Suspension (Teker)")]
        [Tooltip("Tekerin yerden uzanma mesafesi (m).")]
        [Min(0.05f)] public float suspensionDistance = 0.3f;

        [Tooltip("Yay saglamligi. Buyuk = sert, kucuk = yumuşak ama cok ziplar.")]
        [Min(1f)] public float suspensionSpring = 35000f;

        [Tooltip("Sondurme katsayisi. Dengesiz degerler tekerin ziplamasina yol acar.")]
        [Min(1f)] public float suspensionDamper = 4500f;

        [Tooltip("Teker yaricapı (m).")]
        [Range(0.1f, 1f)] public float wheelRadius = 0.35f;

        private void OnValidate()
        {
            // Tutarsiz degerleri guvenli araliga cek: Inspector'da yanlislikla
            // sifir yazilirsa matematik bolme/1e-sifir hatasi uretmesin.
            if (maxSpeed < 1f) maxSpeed = 1f;
            if (maxReverseSpeed < 1f) maxReverseSpeed = 1f;
            if (wheelRadius < 0.05f) wheelRadius = 0.05f;
            if (mass < 100f) mass = 100f;
        }
    }
}
