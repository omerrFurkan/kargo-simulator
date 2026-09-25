using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// ArcadeVehicleController'in fizik matematiği.
    ///
    /// Bu fonksiyonlar static ve saf (saf C#) olduklari icin sahne
    /// acmadan, saniyeler icinde dogrulanabilir. Asama 2'nin en kritik
    /// kararlari burada: hiz limiti, donus kazanimi, fren mantigi.
    /// </summary>
    public sealed class ArcadeVehiclePhysicsTests
    {
        // ------------------------------------------------------------------
        // ComputeSteerAngle: hiz arttikca donus acisi azalmali
        // ------------------------------------------------------------------

        [Test]
        public void Direksiyon_DuzGirdideSifirAciVerir()
        {
            Assert.AreEqual(0f,
                ArcadeVehicleController.ComputeSteerAngle(0f, 0f, 32f, 30f, 0.35f), 0.0001f);
            Assert.AreEqual(0f,
                ArcadeVehicleController.ComputeSteerAngle(0f, 20f, 32f, 30f, 0.35f), 0.0001f);
        }

        [Test]
        public void Direksiyon_DururkenTamAciVerir()
        {
            float angle = ArcadeVehicleController.ComputeSteerAngle(1f, 0f, 32f, 30f, 0.35f);
            Assert.AreEqual(32f, angle, 0.01f, "Hiz sifirken azami aci kullanilmalidir.");
        }

        [Test]
        public void Direksiyon_YonIsaretiTersineCevrilmez()
        {
            float right = ArcadeVehicleController.ComputeSteerAngle(1f, 0f, 32f, 30f, 0.35f);
            float left = ArcadeVehicleController.ComputeSteerAngle(-1f, 0f, 32f, 30f, 0.35f);

            Assert.Greater(right, 0f);
            Assert.Less(left, 0f);
            Assert.AreEqual(-right, left, 0.0001f, "Sol ve sag simetrik olmali.");
        }

        [Test]
        public void Direksiyon_HizArtikcaKisalir()
        {
            float atRest = ArcadeVehicleController.ComputeSteerAngle(1f, 0f, 32f, 30f, 0.35f);
            float at20 = ArcadeVehicleController.ComputeSteerAngle(1f, 20f, 32f, 30f, 0.35f);
            float at60 = ArcadeVehicleController.ComputeSteerAngle(1f, 60f, 32f, 30f, 0.35f);

            Assert.Less(at20, atRest, "20 m/s'te donus acisi durusten kucuk olmali.");
            Assert.Less(at60, at20, "60 m/s'te 20 m/s'ten daha kucuk olmali.");
        }

        [Test]
        public void Direksiyon_HizaGoreAslaBuyumez()
        {
            // Regresyon testi: negatif "hiz kazanimi" hizi ARTIRIRSA
            // araç hizlandikça kontrolü KAYBETMEK yerine artar (klasik hata).
            float previous = float.MaxValue;
            for (float speed = 0f; speed <= 120f; speed += 5f)
            {
                float angle = ArcadeVehicleController.ComputeSteerAngle(1f, speed, 32f, 30f, 0.35f);
                Assert.LessOrEqual(angle, previous + 0.0001f,
                    "Hiz " + speed + " m/s'te donus acisi artti - regresyon!");
                previous = angle;
            }
        }

        [Test]
        public void Direksiyon_YuksekHizdaDondurmeyeDevamEder()
        {
            // Regresyon testi. Eski egri 1/(1 + hiz*k) idi ve 10 m/s'te
            // aciyi %9'a (2.9 derece) dusuruyordu; arac pratikte yon
            // donduremiyordu. Su anki lineer enterpolasyonda 10 m/s'te
            // aci 32 * 0.79 = 25 derece olmali - yani kesinlikle anlamli
            // bir donus kalmali.
            float at10 = ArcadeVehicleController.ComputeSteerAngle(1f, 10f, 32f, 30f, 0.35f);

            Assert.Greater(at10, 20f,
                "10 m/s'te donus acisi 20 dereceden kucuk: arac hizliyken " +
                "yön donduremez. Egriyi gozden gecir.");

            // Azami hizda bile tamamen donmus olmamali
            float atMax = ArcadeVehicleController.ComputeSteerAngle(1f, 30f, 32f, 30f, 0.35f);
            Assert.Greater(atMax, 8f, "Azami hizda donus tamamen kaybolmamali.");
        }

        [Test]
        public void Direksiyon_SifirKazanimHizdenEtkilenmez()
        {
            // steerFalloff = 0 ise "daima tam aci" modu.
            float atRest = ArcadeVehicleController.ComputeSteerAngle(1f, 0f, 32f, 30f, 1f);
            float at60 = ArcadeVehicleController.ComputeSteerAngle(1f, 60f, 32f, 30f, 1f);
            Assert.AreEqual(atRest, at60, 0.0001f);
        }

        // ------------------------------------------------------------------
        // ComputeMotorTorque: hiz limiti
        // ------------------------------------------------------------------

        [Test]
        public void Motor_GazVerilmedigindeCekimYoktur()
        {
            Assert.AreEqual(0f,
                ArcadeVehicleController.ComputeMotorTorque(0f, 0f, 30f, 12f, 3000f), 0.001f);
        }

        [Test]
        public void Motor_DururkenTamCekimVerir()
        {
            float torque = ArcadeVehicleController.ComputeMotorTorque(1f, 0f, 30f, 12f, 3000f);
            Assert.AreEqual(3000f, torque, 0.01f);
        }

        [Test]
        public void Motor_AzamiHizdeCekimSifiraDuser()
        {
            float torque = ArcadeVehicleController.ComputeMotorTorque(1f, 30f, 30f, 12f, 3000f);
            Assert.AreEqual(0f, torque, 0.01f,
                "Azami hizde cekim sifir olmali; yoksa araç hizi asmaya devam eder.");
        }

        [Test]
        public void Motor_AzamiHiziAsanHizdeCekimNegatifOlmaz()
        {
            // Regresyon: hiz limiti asildiysa cekim ASLA negatif olmamali,
            // yoksa araç kendini geri iter.
            float torque = ArcadeVehicleController.ComputeMotorTorque(1f, 45f, 30f, 12f, 3000f);
            Assert.GreaterOrEqual(torque, 0f, "Hiz limiti asildiysa cekim negatif olmamali.");
        }

        [Test]
        public void Motor_GeriYonIleriYonlimitineGoreDahaYavasAzalir()
        {
            // Ayni hizda: ileri cekimi geri cekiminden kucuk olmali,
            // cunku geri hiz limiti (12) ileri hiz limitinden (30) kucuk.
            const float speed = 10f;
            float forward = ArcadeVehicleController.ComputeMotorTorque(1f, speed, 30f, 12f, 3000f);
            float reverse = ArcadeVehicleController.ComputeMotorTorque(-1f, -speed, 30f, 12f, 3000f);

            Assert.Less(Mathf.Abs(reverse), Mathf.Abs(forward),
                "Geri yonde cekim daha hizli azalmali.");
        }

        [Test]
        public void Motor_GeriVitesLimitiTasincaSifir()
        {
            float torque = ArcadeVehicleController.ComputeMotorTorque(-1f, -12f, 30f, 12f, 3000f);
            Assert.AreEqual(0f, torque, 0.01f);
        }

        [Test]
        public void Motor_ThrottleKismiDoğruOranlanir()
        {
            float full = ArcadeVehicleController.ComputeMotorTorque(1f, 0f, 30f, 12f, 3000f);
            float half = ArcadeVehicleController.ComputeMotorTorque(0.5f, 0f, 30f, 12f, 3000f);
            Assert.AreEqual(full * 0.5f, half, 0.01f);
        }

        // ------------------------------------------------------------------
        // ComputeBrakeTorque: frenleme mantigi
        // ------------------------------------------------------------------

        [Test]
        public void Fren_GazAyniYondeykenYoktur()
        {
            // Ileri giderken gaz basili: fren uygulanmamali.
            float torque = ArcadeVehicleController.ComputeBrakeTorque(1f, 20f, 3500f, false);
            Assert.AreEqual(0f, torque, 0.001f);
        }

        [Test]
        public void Fren_GazBirakilincaUygulanir()
        {
            // Ileri giderken gaz yok: arac yavaslamali.
            float torque = ArcadeVehicleController.ComputeBrakeTorque(0f, 20f, 3500f, false);
            Assert.Greater(torque, 0f, "Gaz birakilinca fren uygulanmali, yoksa arac sonsuza kadar kayar.");
        }

        [Test]
        public void Fren_GeriGazIleriGiderkenUygulanir()
        {
            // Ileri giderken S basili: once fren, geri vites degil.
            float torque = ArcadeVehicleController.ComputeBrakeTorque(-1f, 20f, 3500f, false);
            Assert.Greater(torque, 0f, "Ters yonde throttle fren görevi görür.");
        }

        [Test]
        public void Fren_ElFreniHerZamanUygulanir()
        {
            float torque = ArcadeVehicleController.ComputeBrakeTorque(1f, 20f, 3500f, true);
            Assert.GreaterOrEqual(torque, 3500f, "El freni gaz basiliyken bile frenlemeli.");
        }

        [Test]
        public void Fren_DuruştaUygulanmaz_MotorKazanabilsin()
        {
            // Regresyon testi: arac dururken yerlestirme kaynakli 0.02 m/s'lik
            // geriye kayma olur. "Gaz basili ama hiz ters yonde" kurali
            // devreye girerse teker 3500 Nm ile kilitlenir, motor (550 Nm)
            // kazanamaz ve arac hic kalkamaz. Hiz esiginin altinda fren
            // UYGULANMAMALIDIR.
            Assert.AreEqual(0f,
                ArcadeVehicleController.ComputeBrakeTorque(1f, 0f, 3500f, false), 0.001f,
                "Dururken fren uygulanmamali.");

            Assert.AreEqual(0f,
                ArcadeVehicleController.ComputeBrakeTorque(1f, -0.02f, 3500f, false), 0.001f,
                "Durusa yakin kucuk geri kaymada fren kilitlememeli.");

            Assert.AreEqual(0f,
                ArcadeVehicleController.ComputeBrakeTorque(-1f, 0.02f, 3500f, false), 0.001f,
                "Durusa yakin kucuk ileri kaymada fren kilitlememeli.");
        }

        [Test]
        public void Fren_EsiginUstundeYineUygulanir()
        {
            // Esigin biraz ustunde fren normale donmeli.
            Assert.Greater(
                ArcadeVehicleController.ComputeBrakeTorque(0f, 0.5f, 3500f, false), 0f,
                "Hiz belirginken gaz birakilinca fren uygulanmali.");
        }

        [Test]
        public void Fren_GeriGiderkenGazGeriYonuDogrUFrendir()
        {
            // Geri giderken gaz basili (S): fren degil, hizlanma.
            float torque = ArcadeVehicleController.ComputeBrakeTorque(-1f, -10f, 3500f, false);
            Assert.AreEqual(0f, torque, 0.001f,
                "Geri giderken geri gaz fren degildir.");
        }

        // ------------------------------------------------------------------
        // VehicleDefinition guvenlik agi (OnValidate)
        // ------------------------------------------------------------------

        [Test]
        public void Tanim_GeçersizDegerleriGuvenliAraligaCeker()
        {
            var def = ScriptableObject.CreateInstance<VehicleDefinition>();
            try
            {
                def.maxSpeed = 0f;
                def.maxReverseSpeed = -5f;
                def.wheelRadius = 0f;
                def.mass = 1f;

                // OnValidate yalnizca Inspector'da calisir; test icin elle cagirilir.
                InvokeOnValidate(def);

                Assert.GreaterOrEqual(def.maxSpeed, 1f);
                Assert.GreaterOrEqual(def.maxReverseSpeed, 1f);
                Assert.GreaterOrEqual(def.wheelRadius, 0.05f);
                Assert.GreaterOrEqual(def.mass, 100f);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        private static void InvokeOnValidate(VehicleDefinition def)
        {
            var method = typeof(VehicleDefinition).GetMethod(
                "OnValidate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "OnValidate bulunamadi");
            method.Invoke(def, null);
        }
    }
}
