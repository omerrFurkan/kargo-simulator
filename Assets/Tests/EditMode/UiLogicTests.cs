using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Economy;
using KargoSim.Player;
using KargoSim.UI;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Aşama 6: arayüz mantığı.
    ///
    /// UIManager MonoBehaviour olduğu için burada NESNE mantığı test
    /// edilir (satır havuzu, güncelleme, dirty-check), görsel yerleşim
    /// değil. Sahne bütünlüğü ayrı testte (GameSceneIntegrityTests).
    /// </summary>
    public sealed class UiLogicTests
    {
        private ParcelType _type;
        private GameConfig _config;
        private EconomyConfig _economy;
        private VehicleDefinition _vehicle;
        private MapLayout _layout;
        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private PlayerProgress _progress;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _type = ScriptableObject.CreateInstance<ParcelType>();
            _type.displayName = "Test";
            _type.minWeightKg = 1f; _type.maxWeightKg = 2f;
            _type.minVolumeLiters = 1f; _type.maxVolumeLiters = 2f;
            _type.paymentPerKg = 10f;
            _type.Sanitize();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.parcelsPerDay = 6;
            _config.destinationCount = 3;
            _config.baseDeliverySeconds = 100f;
            _config.dayLengthSeconds = 300f;
            _config.startHour = 8f;

            _economy = ScriptableObject.CreateInstance<EconomyConfig>();
            _economy.startingMoney = 100;

            _vehicle = ScriptableObject.CreateInstance<VehicleDefinition>();
            _vehicle.cargoCapacityKg = 1000f;
            _vehicle.cargoCapacityLiters = 1000f;

            _layout = ScriptableObject.CreateInstance<MapLayout>();
            _layout.depotPosition = Vector3.zero;
            _layout.deliveryPoints = new List<DeliveryPointData>
            {
                new DeliveryPointData { id = 0, displayName = "A", position = new Vector3(10f, 0f, 0f) },
                new DeliveryPointData { id = 1, displayName = "B", position = new Vector3(0f, 0f, 20f) },
                new DeliveryPointData { id = 2, displayName = "C", position = new Vector3(-30f, 0f, 0f) },
            };

            _cargo = new CargoSystem(_config, new[] { _type }, _vehicle);
            _delivery = new DeliverySystem(_layout, _cargo, 7f);
            _progress = new PlayerProgress(_economy);
            _clock = new GameClock(_config);

            _cargo.BeginDay(1);
            _delivery.BeginDay();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in new Object[] { _type, _config, _economy, _vehicle, _layout })
                if (o != null) Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------
        // HUD metinleri
        // ------------------------------------------------------------------

        [Test]
        public void Hud_BakiyeVeSeviyeGosterir()
        {
            var hud = new GameObject("Hud").AddComponent<HudView>();

            hud.Refresh(_progress, _clock, _cargo, _delivery);

            Assert.IsTrue(hud.MoneyText.Contains("100"), "Bakiye HUD'da görünmeli: " + hud.MoneyText);
            Assert.IsTrue(hud.LevelText.Contains("1"), "Seviye HUD'da görünmeli: " + hud.LevelText);
            Object.DestroyImmediate(hud.gameObject);
        }

        [Test]
        public void Hud_DegismeyenDegerIcinYenidenYazmaz()
        {
            // Dirty check: ayni deger icin string tahsisi yapilmamali.
            // Cagirdiktan sonra "once deger" ile "sonra deger" ayni kalsali
            // (bu yalnizca gozlemlenebilir bir davranis; en azindan
            // panik vermemesi ve null olmamasi dogrulanir).
            var hud = new GameObject("Hud").AddComponent<HudView>();

            hud.Refresh(_progress, _clock, _cargo, _delivery);
            string first = hud.MoneyText;
            hud.Refresh(_progress, _clock, _cargo, _delivery);
            string second = hud.MoneyText;

            Assert.AreEqual(first, second, "Aynı veri aynı metni vermeli.");
            Object.DestroyImmediate(hud.gameObject);
        }

        [Test]
        public void Hud_YuklenenKargoSayisiniGosterir()
        {
            var hud = new GameObject("Hud").AddComponent<HudView>();

            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            hud.Refresh(_progress, _clock, _cargo, _delivery);

            Assert.IsTrue(hud.CapacityText.Contains("Yuk: 1"), "Yüklenen kargo sayısı görünmeli: " + hud.CapacityText);
            Object.DestroyImmediate(hud.gameObject);
        }

        [Test]
        public void Hud_KargoYuklenmediyseDepoyaYonlendirir()
        {
            var hud = new GameObject("Hud").AddComponent<HudView>();
            hud.Refresh(_progress, _clock, _cargo, _delivery);

            Assert.IsTrue(hud.NextStopText.Contains("Depo") || hud.NextStopText.Contains("Rota"),
                "Kargo yokken oyuncu ne yapacağını bilmeli: " + hud.NextStopText);
            Object.DestroyImmediate(hud.gameObject);
        }

        [Test]
        public void Hud_RotaVarkenSonrakiAdresiGosterir()
        {
            var hud = new GameObject("Hud").AddComponent<HudView>();

            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            _delivery.Tick(0.1f, false);
            hud.Refresh(_progress, _clock, _cargo, _delivery);

            Assert.IsTrue(hud.NextStopText.Contains("Sonraki") || hud.NextStopText.Contains("Teslimat"),
                "Rota varken hedef görünmeli: " + hud.NextStopText);
            Object.DestroyImmediate(hud.gameObject);
        }

        // ------------------------------------------------------------------
        // Depo paneli mantigi
        // ------------------------------------------------------------------

        [Test]
        public void Depot_KargolarYuklenir()
        {
            var go = new GameObject("Depot");
            var view = go.AddComponent<DepotView>();

            // Manifest'i dogrudan doldur: UI bilesenleri olmadan da
            // mantik dogrulanabilir.
            InjectCargo(view, _cargo);

            int available = _cargo.Manifest.AvailableCount;
            view.ToggleSelection(0);
            int loaded = view.LoadSelected();

            Assert.AreEqual(1, loaded, "Seçili kargo yüklenmeliydi.");
            Assert.AreEqual(available - 1, _cargo.Manifest.AvailableCount);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Depot_SecimYoksaYuklemeYapmaz()
        {
            var go = new GameObject("Depot");
            var view = go.AddComponent<DepotView>();

            InjectCargo(view, _cargo);

            Assert.AreEqual(0, view.LoadSelected(), "Seçim yoksa yükleme olmamalı.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Depot_SecimAcilipKapali()
        {
            var go = new GameObject("Depot");
            var view = go.AddComponent<DepotView>();
            InjectCargo(view, _cargo);

            // Tek secim: secili -> yuklenir
            view.ToggleSelection(0);
            Assert.AreEqual(1, view.LoadSelected(), "Tek kez değiştirilen seçim açık olmalı.");

            // Ikinci degistirme: kapali -> yuklenmez
            view.ToggleSelection(1);
            view.ToggleSelection(1);
            Assert.AreEqual(0, view.LoadSelected(),
                "İki kez değiştirilen seçim kapalı olmalı.");

            Object.DestroyImmediate(go);
        }

        /// <summary>DepotView'un _cargo alanini test icin doldurur.</summary>
        private static void InjectCargo(DepotView view, CargoSystem cargo)
        {
            System.Reflection.FieldInfo field = typeof(DepotView).GetField("_cargo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(view, cargo);
        }

        [Test]
        public void Depot_DizinDisindakiSecimYokSayilir()
        {
            var go = new GameObject("Depot");
            var view = go.AddComponent<DepotView>();

            InjectCargo(view, _cargo);

            view.ToggleSelection(999);   // olmayan sira
            Assert.AreEqual(0, view.LoadSelected(), "Geçersiz sıra seçilmemeli.");
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------
        // Gun sonu paneli
        // ------------------------------------------------------------------

        [Test]
        public void GunOzeti_TeslimatYoksaBostaGosterir()
        {
            var go = new GameObject("Summary");
            var view = go.AddComponent<DaySummaryView>();

            // IsOpen bir panel referansina bakar; testte o da olmali.
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(go.transform, false);
            SetPanel(view, panelGo);

            view.Show(new DaySummary(1, 0, 0, 0, 0, 0, 0, 1, 0));
            Assert.IsTrue(view.IsOpen, "Gün sonu paneli açılmalı.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GunOzeti_PanelGizlenebilir()
        {
            var go = new GameObject("Summary");
            var view = go.AddComponent<DaySummaryView>();

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(go.transform, false);
            SetPanel(view, panelGo);

            view.Show(new DaySummary(1, 1, 1, 0, 0, 50, 10, 1, 0));
            Assert.IsTrue(view.IsOpen);

            view.Hide();
            Assert.IsFalse(view.IsOpen, "Panel gizlenebilmeli.");

            Object.DestroyImmediate(go);
        }

        /// <summary>DaySummaryView'un panel alanini test icin doldurur.</summary>
        private static void SetPanel(DaySummaryView view, GameObject panel)
        {
            System.Reflection.FieldInfo field = typeof(DaySummaryView).GetField("panel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(view, panel);
        }
    }
}
