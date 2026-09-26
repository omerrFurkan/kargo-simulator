using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// BISIKLET SURUS FIZIGI OLÇUM ARACI (Asama 14 tuning).
    ///
    /// AMAC: TUNING'I TAHMIN ETMEK DEGIL, OLCMEK.
    ///
    /// Bu test fizik motorunu GERCEKTEN calistirir (WaitForFixedUpdate)
    /// ve bisikletin davranisini sayilara doker:
    ///   - azami hiz
    ///   - 0 -> 20 km/sa kac saniyede
    ///   - fren mesafesi
    ///   - tam gaz + tam direksiyonda DEVRI LIYOR MU
    ///   - duz yolda kayma var mi
    ///
    /// NEDEN GEREKLI: `antiRollStrength` ve `uprightStrength` degerleri
    /// gorsel test yapilmadan tahmin edilmisti. Sayisal olcum, oyuncunun
    /// "devriliyor / cok hizli / dondurulemez" demesinden once
    /// nesnel bir referans verir.
    ///
    /// BU TEST BASARILI OLMAK ZORUNDA DEGILDIR: amaci OLCMEKTIR.
    /// Asagidaki esik degerler "hedef"tir; sapma raporlanir.
    /// </summary>
    public class BicyclePhysicsMeasurements
    {
        private GameObject _bikeGo;
        private GameObject _groundGo;
        private Vehicle.BicycleController _bike;
        private Vehicle.VehicleDefinition _def;
        private StringBuilder _log;

        private const float MeasureSeconds = 6f;
        private const float TargetSpeedKmh = 30f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_bikeGo != null) Object.DestroyImmediate(_bikeGo);
            if (_groundGo != null) Object.DestroyImmediate(_groundGo);
            if (_def != null) Object.DestroyImmediate(_def);
            _bikeGo = null;
            _groundGo = null;
            _bike = null;
            _def = null;

            yield return null;
        }

        /// <summary>Yalnizca zemin + bisikliKur. Gercek sahne kullanmaz.</summary>
        private IEnumerator BuildBike()
        {
            _def = ScriptableObject.CreateInstance<Vehicle.VehicleDefinition>();
            _def.mass = 180f;
            _def.centerOfMassHeight = 0.32f;
            _def.maxSpeed = 6.8f;          // m/s = 24.5 km/h (TUNED)
            _def.maxReverseSpeed = 2.5f;   // m/s ≈ 9 km/h
            _def.engineForce = 1400f;
            _def.brakeForce = 1800f;
            _def.handbrakeStrength = 2f;
            _def.maxSteerAngle = 38f;
            _def.minSteerFactor = 0.35f;
            _def.wheelRadius = 0.32f;
            _def.suspensionDistance = 0.15f;
            _def.suspensionSpring = 14000f;
            _def.suspensionDamper = 2200f;

            // Zemin: genis kutu. Plane kullanilmaz (MeshCollider + teker
            // kombinasyonu guvenilir degil).
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(0f, -0.5f, 0f);
            var ground = _groundGo.AddComponent<BoxCollider>();
            ground.size = new Vector3(400f, 1f, 400f);

            _bikeGo = new GameObject("MeasureBike");
            _bikeGo.transform.position = new Vector3(0f, 1.2f, 0f);

            var rb = _bikeGo.AddComponent<Rigidbody>();
            rb.mass = 180f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.angularDrag = 4f;
            rb.drag = 0.2f;

            var chassis = _bikeGo.AddComponent<BoxCollider>();
            chassis.center = new Vector3(0f, 0.86f, -0.35f);
            chassis.size = new Vector3(0.80f, 0.46f, 2.20f);

            _bike = _bikeGo.AddComponent<Vehicle.BicycleController>();

            var front = MakeWheel("Wheel_Front", new Vector3(0f, 0.32f, 0.78f), true);
            var rearL = MakeWheel("Wheel_RearLeft", new Vector3(-0.20f, 0.32f, -0.72f), false);
            var rearR = MakeWheel("Wheel_RearRight", new Vector3(0.20f, 0.32f, -0.72f), false);

            _bike.ConfigureForSetup(_def, front, rearL, rearR);
            _bikeGo.SetActive(false);
            _bike.ConfigureForSetup(_def, front, rearL, rearR);
            _bikeGo.SetActive(true);

            // ControlsLocked = false: gaz/direksiyon uygulansin.
            _bike.ControlsLocked = false;

            _log = new StringBuilder();
            yield return null;
        }

        private WheelCollider MakeWheel(string name, Vector3 localPos, bool steerable)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_bikeGo.transform, false);
            go.transform.localPosition = localPos;

            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.32f;
            wc.suspensionDistance = 0.15f;
            wc.wheelDampingRate = 0.3f;
            wc.forceAppPointDistance = 0f;

            var spring = wc.suspensionSpring;
            spring.spring = 14000f;
            spring.damper = 2200f;
            wc.suspensionSpring = spring;

            return wc;
        }

        /// <summary>
        /// Bisiklete girdi yazar.
        ///
        /// ONCEKIL YANLIS YOL: test, fizik adimindan hemen once tekerlere
        /// dogrudan motorTorque yaziyordu. Ama Unity hemen ardindan
        /// controller'in FixedUpdate'ini cagirip torku SIFIRLIYORDU.
        /// Sonuc: olcum "0 km/h" cikiyordu ve test bunu "devrilmedi" diye
        /// GECIYORDU. Test sahte bir yol olcuyordu.
        ///
        /// DOGRU YOL: SetTestInput ile klavyeyi taklit ediyoruz.
        /// FixedUpdate -> ReadInput -> tork/fren/direksiyon yolu klavyeyle
        /// birebir ayni calisir.
        /// </summary>
        private static void Feed(Vehicle.BicycleController bike,
                                 float vertical, float horizontal,
                                 float handbrake, float reverseBrake)
        {
            bike.SetTestInput(vertical, horizontal, handbrake, reverseBrake);
        }

        /// <summary>Tekerin altindaki zemin yuksekligi (yoksa -1).</summary>
        private float GroundY(WheelCollider wc)
        {
            Vector3 origin = wc.transform.position + Vector3.up * 1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y - (wc.transform.position.y - wc.radius);
            }
            return -1f;
        }

        private float GroundTopY()
        {
            return _groundGo.transform.position.y + _groundGo.GetComponent<BoxCollider>().size.y * 0.5f;
        }

        [UnityTest]
        public IEnumerator DuzYol_HizVeTepkiOlc()
        {
            yield return BuildBike();

            // Zemine otur.
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            // TEKER TEMAS TEŞHISI: hareket etmemenin sebebi ya tork
            // ulasmiyor ya da tekerler yere degmiyor. Hangisi oldugunu
            // tahmin etmeyelim, olcelelim.
            _log.AppendLine("=== TEKER TEMASI (gaz oncesi) ===");
            _log.AppendLine("  govde Y      : " + _bikeGo.transform.position.y.ToString("F3"));
            var diagWheels = _bikeGo.GetComponentsInChildren<WheelCollider>();
            for (int w = 0; w < diagWheels.Length; w++)
            {
                var wc = diagWheels[w];
                _log.AppendLine("  " + wc.name
                    + " grounded=" + wc.isGrounded
                    + " Y=" + wc.transform.position.y.ToString("F3")
                    + " altY=" + (wc.transform.position.y - wc.radius).ToString("F3")
                    + " motor=" + wc.motorTorque.ToString("F0")
                    + " fren=" + wc.brakeTorque.ToString("F0")
                    + " hitGround=" + GroundY(wc));
            }
            _log.AppendLine("  zemin ust Y  : " + (GroundTopY() - 0.5f).ToString("F3") + " (kutu ust yuzeyi)");
            Debug.Log(_log.ToString());

            var rb = _bikeGo.GetComponent<Rigidbody>();
            float startZ = rb.position.z;
            float timeTo20 = -1f;
            float t = 0f;
            float topSpeed = 0f;
            float maxRoll = 0f;
            float maxPitch = 0f;
            float lateralDrift = 0f;

            int steps = Mathf.RoundToInt(MeasureSeconds / Time.fixedDeltaTime);

            for (int i = 0; i < steps; i++)
            {
                Feed(_bike, 1f, 0f, 0f, 0f);
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;

                float kmh = rb.linearVelocity.magnitude * 3.6f;
                if (kmh > topSpeed) topSpeed = kmh;
                if (timeTo20 < 0f && kmh >= 20f) timeTo20 = t;

                // Yatay egim (roll) ve dikey egim (pitch): devrilme
                // belirteci. 60 derece uzeri "tilted".
                float roll = Vector3.SignedAngle(_bikeGo.transform.up, Vector3.up,
                                                _bikeGo.transform.forward);
                float pitch = Vector3.SignedAngle(_bikeGo.transform.up, Vector3.up,
                                                 _bikeGo.transform.right);
                if (Mathf.Abs(roll) > Mathf.Abs(maxRoll)) maxRoll = roll;
                if (Mathf.Abs(pitch) > Mathf.Abs(maxPitch)) maxPitch = pitch;

                // Yanal kayma: hizin yanal bileseni / toplam hiz.
                float lateral = Vector3.Dot(rb.linearVelocity, _bikeGo.transform.right);
                lateralDrift = Mathf.Max(lateralDrift, Mathf.Abs(lateral));
            }

            float distance = rb.position.z - startZ;
            float upsideDown = Vector3.Dot(_bikeGo.transform.up, Vector3.up);

            _log.AppendLine("=== DUZ YOL (tam gaz, 6 sn) ===");
            _log.AppendLine("  azami hiz     : " + topSpeed.ToString("F1") + " km/h");
            _log.AppendLine("  0->20 km/h    : " + (timeTo20 < 0f ? "HIZLANMADI" : timeTo20.ToString("F2") + " sn"));
            _log.AppendLine("  6 sn yol      : " + distance.ToString("F1") + " m");
            _log.AppendLine("  azami yanal kayma: " + lateralDrift.ToString("F2") + " m/s");
            _log.AppendLine("  azami dik egim : " + maxPitch.ToString("F1") + " derece");
            _log.AppendLine("  azami yatay egim: " + maxRoll.ToString("F1") + " derece");
            _log.AppendLine("  son diklik (up.y): " + upsideDown.ToString("F2")
                            + (upsideDown > 0.5f ? "  -> DUZDE" : "  -> DEVRILDI!"));
            _log.AppendLine("  tanim maxSpeed : " + _def.maxSpeed + " (motor matematiSi bunu m/s "
                            + "olarak kullanir; = " + (_def.maxSpeed * 3.6f).ToString("F1") + " km/h)");

            Debug.Log(_log.ToString());

            // Once ASIL KONTROL: bisiklet HAREKET ETMELI. Onceki surumde
            // bu kontrol yoktu ve 0 km/h olcumleri "devrilmedi" diye gecti.
            Assert.Greater(topSpeed, 10f,
                "BISIKLET HAREKET ETMEDI. Tam gazda bile " 
                + topSpeed.ToString("F1") + " km/h. Tork tekerlere "
                + "ulasmiyor veya tekerler yere temas etmiyor.");

            Assert.Greater(distance, 20f,
                "6 snde " + distance.ToString("F1") + " m yol alindi; "
                + "beklenen cok daha fazla.");

            // Devrilmemeli.
            Assert.Greater(upsideDown, 0.5f,
                "BISIKLET DUZ YOLDA DEVRILDI. Stability degerleri "
                + "(antiRollStrength/uprightStrength) ayarlanmali.");
            // Hedef: kargo bisikleti 30 km/sa civari.
            Assert.Less(topSpeed, TargetSpeedKmh + 12f,
                "Bisiklet cok hizli: " + topSpeed.ToString("F1")
                + " km/h. Kargo bisikleti icin " + TargetSpeedKmh + " km/sa hedeflenmisti.");
        }

        [UnityTest]
        public IEnumerator TamDireksiyon_DevrilmeVeKontrolOlc()
        {
            yield return BuildBike();

            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            // Once hizlan, sonra tam gaz + tam direksiyon.
            for (int i = 0; i < Mathf.RoundToInt(3f / Time.fixedDeltaTime); i++)
            {
                Feed(_bike, 1f, 0f, 0f, 0f);
                yield return new WaitForFixedUpdate();
            }

            float entrySpeed = _bikeGo.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f;
            float worstUp = 1f;
            float maxYawRate = 0f;
            int steps = Mathf.RoundToInt(3f / Time.fixedDeltaTime);

            for (int i = 0; i < steps; i++)
            {
                Feed(_bike, 1f, 1f, 0f, 0f);   // tam gaz + TAM DIREKSIYON
                yield return new WaitForFixedUpdate();

                float up = Vector3.Dot(_bikeGo.transform.up, Vector3.up);
                if (up < worstUp) worstUp = up;

                float yawRate = Mathf.Abs(_bikeGo.GetComponent<Rigidbody>().angularVelocity.y);
                if (yawRate > maxYawRate) maxYawRate = yawRate;
            }

            _log.AppendLine("=== VIRAJ (tam gaz + tam direksiyon, 3 sn) ===");
            _log.AppendLine("  giris hizi   : " + entrySpeed.ToString("F1") + " km/h");
            _log.AppendLine("  en kotu diklik: up.y = " + worstUp.ToString("F2")
                            + (worstUp > 0.3f ? "  -> kontrol korundu" : "  -> DEVRILEBILIR"));
            _log.AppendLine("  azami yat donus hizi: " + maxYawRate.ToString("F2") + " rad/s");
            Debug.Log(_log.ToString());

            Assert.Greater(worstUp, 0.3f,
                "Tam yanda donus bisikleti yatirdi (up.y = " + worstUp.ToString("F2")
                + "). Stability degerleri ayarlanmali.");
        }

        [UnityTest]
        public IEnumerator Fren_MesafeOlc()
        {
            yield return BuildBike();

            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            // 25 km/h'e kadar hizlan.
            var rb = _bikeGo.GetComponent<Rigidbody>();
            for (int i = 0; i < Mathf.RoundToInt(4f / Time.fixedDeltaTime); i++)
            {
                Feed(_bike, 1f, 0f, 0f, 0f);
                yield return new WaitForFixedUpdate();
                if (rb.linearVelocity.magnitude * 3.6f >= 25f) break;
            }

            float entrySpeed = rb.linearVelocity.magnitude * 3.6f;
            float startZ = rb.position.z;
            float t = 0f;
            bool stopped = false;

            for (int i = 0; i < Mathf.RoundToInt(5f / Time.fixedDeltaTime); i++)
            {
                Feed(_bike, 0f, 0f, 1f, 0f);   // EL FRENI (Space)
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;

                if (rb.linearVelocity.magnitude * 3.6f < 1f) { stopped = true; break; }
            }

            float brakeDistance = rb.position.z - startZ;

            _log.AppendLine("=== FREN (Space, el freni) ===");
            _log.AppendLine("  giris hizi   : " + entrySpeed.ToString("F1") + " km/h");
            _log.AppendLine("  fren mesafesi: " + brakeDistance.ToString("F2") + " m");
            _log.AppendLine("  durma suresi : " + t.ToString("F2") + " sn  "
                            + (stopped ? "(durdu)" : "(DURMADI)"));
            Debug.Log(_log.ToString());

            Assert.IsTrue(stopped,
                "El freni bisikleti DURDURMADI. fren kuvveti yetersiz.");
        }

        [UnityTest]
        public IEnumerator Geri_GeriHareketOlc()
        {
            yield return BuildBike();

            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            var rb = _bikeGo.GetComponent<Rigidbody>();
            float startZ = rb.position.z;
            float reverseTop = 0f;

            for (int i = 0; i < Mathf.RoundToInt(4f / Time.fixedDeltaTime); i++)
            {
                Feed(_bike, -1f, 0f, 0f, 1f);   // geri gaz (S)
                yield return new WaitForFixedUpdate();

                float back = -Vector3.Dot(rb.linearVelocity, _bikeGo.transform.forward) * 3.6f;
                if (back > reverseTop) reverseTop = back;
            }

            _log.AppendLine("=== GERI (S basili, 4 sn) ===");
            _log.AppendLine("  azami geri hiz: " + reverseTop.ToString("F1") + " km/h");
            _log.AppendLine("  konum degisimi : " + (rb.position.z - startZ).ToString("F2") + " m");
            Debug.Log(_log.ToString());

            Assert.Less(rb.position.z, startZ,
                "S basiliyken bisiklet GERI GITMEDI.");
        }
    }
}
